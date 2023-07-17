﻿using ERBingoRandomizer.FileHandler;
using ERBingoRandomizer.Params;
using ERBingoRandomizer.Utility;
using FSParam;
using SoulsFormats;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ERBingoRandomizer.Utility.Const;
using static ERBingoRandomizer.Utility.Config;
using static FSParam.Param;

namespace ERBingoRandomizer.Randomizer;

public partial class BingoRandomizer {
    //static async method that behave like a constructor       
    public static async Task<BingoRandomizer> BuildRandomizerAsync(string path, string seed, CancellationToken cancellationToken) {
        BingoRandomizer rando = new(path, seed, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Run(() => rando.init());
        return rando;
    }
    private Task init() {
        if (!allCacheFilesExist()) {
            _bhd5Reader = new BHD5Reader(_path, CacheBHDs, _cancellationToken);
        }
        _cancellationToken.ThrowIfCancellationRequested();
        _oodlePtr = Kernel32.LoadLibrary($"{_path}/oo2core_6_win64.dll");
        _cancellationToken.ThrowIfCancellationRequested();
        getDefs();
        _cancellationToken.ThrowIfCancellationRequested();
        getParams();
        _cancellationToken.ThrowIfCancellationRequested();
        getFmgs();
        _cancellationToken.ThrowIfCancellationRequested();
        buildDictionaries();
        _cancellationToken.ThrowIfCancellationRequested();
        Kernel32.FreeLibrary(_oodlePtr);
        return Task.CompletedTask;
    }
    private bool allCacheFilesExist() {
        return File.Exists(ItemMsgBNDPath) && File.Exists(MenuMsgBNDPath);
    }
    private void getDefs() {
        _paramDefs = new List<PARAMDEF>();
        string[] defs = Util.GetResources("Params/Defs");
        foreach (string def in defs) {
            _paramDefs.Add(Util.XmlDeserialize(def));
        }
    }
    private void getParams() {
        _regulationBnd = SFUtil.DecryptERRegulation(_regulationPath);
        foreach (BinderFile file in _regulationBnd.Files) {
            getParams(file);
        }
    }
    private void getFmgs() {
        byte[] itemMsgbndBytes = getOrOpenFile(ItemMsgBNDPath);
        if (itemMsgbndBytes == null) {
            throw new InvalidFileException(ItemMsgBNDPath);
        }
        BND4 itemBnd = BND4.Read(itemMsgbndBytes);
        foreach (BinderFile file in itemBnd.Files) {
            getFmgs(file);
        }

        _cancellationToken.ThrowIfCancellationRequested();

        byte[] menuMsgbndBytes = getOrOpenFile(MenuMsgBNDPath);
        if (itemMsgbndBytes == null) {
            throw new InvalidFileException(MenuMsgBNDPath);
        }
        _menuMsgBND = BND4.Read(menuMsgbndBytes);
        foreach (BinderFile file in _menuMsgBND.Files) {
            getFmgs(file);
        }
    }
    private byte[] getOrOpenFile(string path) {
        if (!File.Exists($"{CachePath}/{path}")) {
            byte[]? file = _bhd5Reader.GetFile(path);
            Directory.CreateDirectory(Path.GetDirectoryName($"{CachePath}/{path}"));
            File.WriteAllBytes($"{CachePath}/{path}", file);
            return file;
        }

        return File.ReadAllBytes($"{CachePath}/{path}");
    }
    private void buildDictionaries() {
        _weaponDictionary = new Dictionary<int, EquipParamWeapon>();
        _weaponTypeDictionary = new Dictionary<ushort, List<Row>>();
        _weaponNameDictionary = new Dictionary<int, string>();

        foreach (Row row in _equipParamWeapon.Rows) {
            string rowString = _weaponFmg[row.ID];
            if ((int)row["sortId"].Value.Value == 9999999 || row.ID == 17030000 || string.IsNullOrWhiteSpace(rowString) || rowString.ToLower().Contains("error")) {
                continue;
            }

            _weaponNameDictionary[row.ID] = rowString;
            EquipParamWeapon wep = new(row);
            if (!Enumerable.Range(81, 86).Contains(wep.wepType)) {
                _weaponDictionary.Add(row.ID, new EquipParamWeapon(row));
            }

            List<Row>? rows;
            if (_weaponTypeDictionary.TryGetValue(wep.wepType, out rows)) {
                rows.Add(row);
            }
            else {
                rows = new List<Row>();
                rows.Add(row);
                _weaponTypeDictionary.Add(wep.wepType, rows);
            }
        }

        _customWeaponDictionary = new();

        foreach (Row row in _equipParamCustomWeapon.Rows) {
            if (!_weaponDictionary.TryGetValue((int)row["baseWepId"].Value.Value, out EquipParamWeapon wep)) {
                continue;
            }
            EquipParamCustomWeapon customWep = new(row);
            _weaponNameDictionary[row.ID] = $"{_weaponNameDictionary[customWep.baseWepId]} +{customWep.reinforceLv}";
            _customWeaponDictionary.Add(row.ID, wep);

            List<Row>? rows;
            if (_weaponTypeDictionary.TryGetValue(wep.wepType, out rows)) {
                rows.Add(row);
            }
            else {
                rows = new List<Row>();
                rows.Add(row);
                _weaponTypeDictionary.Add(wep.wepType, rows);
            }
        }

        _armorTypeDictionary = new Dictionary<byte, List<Row>>();
        foreach (Row row in _equipParamProtector.Rows) {
            int sortId = (int)row["sortId"].Value.Value;
            string rowString = _protectorFmg[row.ID];
            if (sortId == 9999999 || sortId == 99999 || string.IsNullOrWhiteSpace(rowString) || rowString.ToLower().Contains("error")) {
                continue;
            }

            byte protectorCategory = (byte)row["protectorCategory"].Value.Value;
            List<Row>? rows;
            if (_armorTypeDictionary.TryGetValue(protectorCategory, out rows)) {
                rows.Add(row);
            }
            else {
                rows = new List<Row> {
                    row,
                };
                _armorTypeDictionary.Add(protectorCategory, rows);
            }
        }

        _goodsDictionary = new Dictionary<int, Row>();
        foreach (Row row in _equipParamGoods.Rows) {
            int sortId = (int)row["sortId"].Value.Value;
            string rowString = _goodsFmg[row.ID];
            if (sortId == 9999999 || sortId == 0 || string.IsNullOrWhiteSpace(rowString) || rowString.ToLower().Contains("error")) {
                continue;
            }

            _goodsDictionary.Add(row.ID, row);
        }

        _magicDictionary = new Dictionary<int, Magic>();
        _magicTypeDictionary = new Dictionary<byte, List<Row>>();
        foreach (Row row in _magicParam.Rows) {
            if (!_goodsDictionary.ContainsKey(row.ID)) {
                continue;
            }
            Magic magic = new(row);
            _magicDictionary.Add(row.ID, magic);
            List<Row>? rows;
            if (_magicTypeDictionary.TryGetValue(magic.ezStateBehaviorType, out rows)) {
                rows.Add(row);
            }
            else {
                rows = new List<Row>();
                rows.Add(row);
                _magicTypeDictionary.Add(magic.ezStateBehaviorType, rows);
            }
        }
    }
    private void getParams(BinderFile file) {
        string fileName = Path.GetFileName(file.Name);
        switch (fileName) {
            case EquipParamWeaponName:
                _equipParamWeapon = Param.Read(file.Bytes);
                if (!_equipParamWeapon.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_equipParamWeapon.ParamType);
                }
                break;
            case EquipParamCustomWeaponName:
                _equipParamCustomWeapon = Param.Read(file.Bytes);
                if (!_equipParamCustomWeapon.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_equipParamCustomWeapon.ParamType);
                }
                break;
            case EquipParamGoodsName:
                _equipParamGoods = Param.Read(file.Bytes);
                if (!_equipParamGoods.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_equipParamGoods.ParamType);
                }
                break;
            case EquipParamProtectorName:
                _equipParamProtector = Param.Read(file.Bytes);
                if (!_equipParamProtector.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_equipParamProtector.ParamType);
                }
                break;
            case CharaInitParamName:
                _charaInitParam = Param.Read(file.Bytes);
                if (!_charaInitParam.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_charaInitParam.ParamType);
                }
                break;
            case MagicName:
                _magicParam = Param.Read(file.Bytes);
                if (!_magicParam.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_magicParam.ParamType);
                }
                break;
            case ItemLotParam_mapName:
                _itemLotParam_map = Param.Read(file.Bytes);
                if (!_itemLotParam_map.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_itemLotParam_map.ParamType);
                }
                break;
            case ItemLotParam_enemyName:
                _itemLotParam_enemy = Param.Read(file.Bytes);
                if (!_itemLotParam_enemy.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_itemLotParam_enemy.ParamType);
                }
                break;
            case ShopLineupParamName:
                _shopLineupParam = Param.Read(file.Bytes);
                if (!_shopLineupParam.ApplyParamDefsCarefully(_paramDefs)) {
                    throw new InvalidParamDefException(_shopLineupParam.ParamType);
                }
                break;
        }
    }
    private void getFmgs(BinderFile file) {
        string fileName = Path.GetFileName(file.Name);
        switch (fileName) {
            case WeaponNameName:
                _weaponFmg = FMG.Read(file.Bytes);
                break;
            case ProtectorNameName:
                _protectorFmg = FMG.Read(file.Bytes);
                break;
            case GoodsNameName:
                _goodsFmg = FMG.Read(file.Bytes);
                break;
            case AccessoryNameName:
                _accessoryFmg = FMG.Read(file.Bytes);
                break;
            case GR_LineHelpName:
                _lineHelpFmg = FMG.Read(file.Bytes);
                break;
            case GR_MenuTextName:
                _menuTextFmg = FMG.Read(file.Bytes);
                break;
        }
    }
}