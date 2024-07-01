﻿using Project.Params;
using Project.Settings;
using Project.Utility;
using FSParam;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Project.Tasks;

public partial class Randomizer
{
    private string createSeed()
    {
        return "s" + Random.Shared.NextInt64().ToString() + "d";
    }
    private static int getSeedFromHashData(IEnumerable<byte> hashData)
    {   // TODO visit why "toastx" breaks the app
        IEnumerable<byte[]> chunks = hashData.Chunk(4);
        return chunks.Aggregate(0, (current, chunk) => current ^ BitConverter.ToInt32(chunk));
    }
    private void allocateStatsAndSpells(int rowId, CharaInitParam startingClass, IReadOnlyList<int> spells)
    {
        switch (rowId)
        {
            case 3000:
                setClassStats(startingClass);
                break;
            case 3001:
                setClassStats(startingClass);
                break;
            case 3002:
                setClassStats(startingClass);
                break;
            case 3003:
                setClassStats(startingClass);
                break;
            case 3004:
                setClassStats(startingClass);
                break;
            case 3005:
                setClassStats(startingClass);
                break;
            case 3006:
                setConfessorStats(startingClass);
                guaranteeIncantations(startingClass, spells);
                break;
            case 3007:
                setClassStats(startingClass);
                break;
            case 3008:
                setPrisonerStats(startingClass);
                guaranteeSorceries(startingClass, spells);
                break;
            case 3009:
                setClassStats(startingClass);
                break;
        }
    }
    private void guaranteeSorceries(CharaInitParam chr, IReadOnlyList<int> spells)
    {
        if (hasSpellOfType(chr, Const.SorceryType))
        { return; }

        chr.equipSpell01 = -1;
        chr.equipSpell02 = -1;
        randomizeSorceries(chr, spells);
    }
    private void guaranteeIncantations(CharaInitParam chr, IReadOnlyList<int> spells)
    {
        if (hasSpellOfType(chr, Const.IncantationType))
        { return; }

        chr.equipSpell01 = -1;
        chr.equipSpell02 = -1;
        randomizeIncantations(chr, spells);
    }
    private Dictionary<int, ItemLotEntry> getReplacementHashmap(IOrderedDictionary orderedDictionary)
    {
        Dictionary<int, ItemLotEntry> dict = new();

        List<ItemLotEntry> bows = (List<ItemLotEntry>?)orderedDictionary[(object)Const.BowType] ?? new List<ItemLotEntry>();
        List<ItemLotEntry> lightbows = (List<ItemLotEntry>?)orderedDictionary[(object)Const.LightBowType] ?? new List<ItemLotEntry>();
        List<ItemLotEntry> greatbows = (List<ItemLotEntry>?)orderedDictionary[(object)Const.GreatbowType] ?? new List<ItemLotEntry>();
        List<ItemLotEntry> crossbows = (List<ItemLotEntry>?)orderedDictionary[(object)Const.CrossbowType] ?? new List<ItemLotEntry>();
        List<ItemLotEntry> ballista = (List<ItemLotEntry>?)orderedDictionary[(object)Const.BallistaType] ?? new List<ItemLotEntry>();

        bows.AddRange(lightbows);
        bows.AddRange(greatbows);
        bows.AddRange(crossbows);
        bows.AddRange(ballista);
        orderedDictionary[(object)Const.BowType] = bows;
        orderedDictionary.Remove(Const.LightBowType);
        orderedDictionary.Remove(Const.GreatbowType);
        orderedDictionary.Remove(Const.CrossbowType);
        orderedDictionary.Remove(Const.BallistaType);

        for (int i = 0; i < orderedDictionary.Count; i++)
        {
            List<ItemLotEntry> value = (List<ItemLotEntry>)orderedDictionary[i]!;
            List<ItemLotEntry> itemLotEntries = new(value);
            itemLotEntries.Shuffle(_random);
            foreach (ItemLotEntry entry in itemLotEntries)
            {
                dict.Add(entry.Id, getNewId(entry.Id, value));
            }
        }
        return dict;
    }
    private Dictionary<int, int> getShopReplacementHashmap(IOrderedDictionary orderedDictionary)
    {
        Dictionary<int, int> output = new();
        for (int i = 0; i < orderedDictionary.Count; i++)
        {
            List<int> value = (List<int>)orderedDictionary[i]!;
            List<int> itemLotEntries = new(value);
            itemLotEntries.Shuffle(_random);
            foreach (int entry in itemLotEntries)
            {
                output.Add(entry, getNewId(entry, value));
            }
        }
        return output;
    }
    private void dedupeAndRandomizeVectors(IOrderedDictionary orderedDictionary)
    {
        for (int i = 0; i < orderedDictionary.Count; i++)
        {
            List<ItemLotEntry> values = (List<ItemLotEntry>)orderedDictionary[i]!;
            List<ItemLotEntry> distinct = values.Distinct().ToList();
            distinct.Shuffle(_random);
            orderedDictionary[i] = distinct;
        }
    }
    private void dedupeAndRandomizeShopVectors(IOrderedDictionary orderedDictionary)
    {
        for (int i = 0; i < orderedDictionary.Count; i++)
        {
            List<int> values = (List<int>)orderedDictionary[i]!;
            List<int> distinct = values.Distinct().ToList();
            distinct.Shuffle(_random);
            orderedDictionary[i] = distinct;
        }
    }

    private void replaceShopLineupParam(ShopLineupParam lot, IList<int> shopLineupParamDictionary, IList<ShopLineupParam> shopLineupParamRemembranceList)
    {
        if (lot.mtrlId == -1)
        {
            // int newId = getNewId(lot.equipId, shopLineupParamDictionary);
            // TODO: currently has DLC weapons available, longterm will want DLC items read as params 
            List<int> weapons = Equipment.WeaponShopLists[
                _random.Next(Equipment.WeaponShopLists.Count)
            ];
            int index = _random.Next(weapons.Count);
            int newId = weapons[index];

            logItem($"{_weaponNameDictionary[lot.equipId]} -> {/*_weaponNameDictionary[newId]*/ "Fix Name Dictionary"} : {newId}");
            lot.equipId = newId;
            return;
        }
        ShopLineupParam newRemembrance = getNewId(lot.equipId, shopLineupParamRemembranceList);
        logItem($"{_weaponNameDictionary[lot.equipId]} -> {_weaponNameDictionary[newRemembrance.equipId]}");
        copyShopLineupParam(lot, newRemembrance);
    }
    private void replaceShopLineupParamMagic(ShopLineupParam lot, IReadOnlyDictionary<int, int> shopLineupParamDictionary, IList<ShopLineupParam> shopLineupParamRemembranceList)
    {
        if (lot.mtrlId == -1)
        {
            int newItem = shopLineupParamDictionary[lot.equipId];
            logItem($"{_goodsFmg[lot.equipId]} -> {_goodsFmg[newItem]} : {newItem}");
            lot.equipId = newItem;
            return;
        }
        ShopLineupParam newRemembrance = getNewId(lot.equipId, shopLineupParamRemembranceList);
        logItem($"{_goodsFmg[lot.equipId]} -> {_goodsFmg[newRemembrance.equipId]}");
        copyShopLineupParam(lot, newRemembrance);
    }
    private void addDescriptionString(CharaInitParam chr, int id)
    { // TODO not updating in 1.12, fix
        // List<string> str = new() {
        //     $"{_weaponNameDictionary[chr.wepleft]}{getRequiredLevelsWeapon(chr, chr.wepleft)}",
        //     $"{_weaponNameDictionary[chr.wepRight]}{getRequiredLevelsWeapon(chr, chr.wepRight)}",
        // };
        // if (chr.subWepLeft != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.subWepLeft]}{getRequiredLevelsWeapon(chr, chr.subWepLeft)}");
        // }
        // if (chr.subWepRight != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.subWepRight]}{getRequiredLevelsWeapon(chr, chr.subWepRight)}");
        // }
        // if (chr.subWepLeft3 != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.subWepLeft3]}{getRequiredLevelsWeapon(chr, chr.subWepLeft3)}");
        // }
        // if (chr.subWepRight3 != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.subWepRight3]}{getRequiredLevelsWeapon(chr, chr.subWepRight3)}");
        // }
        // if (chr.equipArrow != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.equipArrow]}[{chr.arrowNum}]");
        // }
        // if (chr.equipSubArrow != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.equipSubArrow]}[{chr.subArrowNum}]");
        // }
        // if (chr.equipBolt != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.equipBolt]}[{chr.boltNum}]");
        // }
        // if (chr.equipSubBolt != -1)
        // {
        //     str.Add($"{_weaponNameDictionary[chr.equipSubBolt]}[{chr.subBoltNum}]");
        // }
        // if (chr.equipSpell01 != -1)
        // {
        //     str.Add($"{_goodsFmg[chr.equipSpell01]}");
        // }
        // if (chr.equipSpell02 != -1)
        // {
        //     str.Add($"{_goodsFmg[chr.equipSpell02]}");
        // }

        // _lineHelpFmg[id] = string.Join(", ", str);
    }
    private void writeFiles()
    {
        if (Directory.Exists(Const.BingoPath))
        {
            Directory.Delete(Const.BingoPath, true);
        }
        Directory.CreateDirectory(Path.GetDirectoryName($"{Const.BingoPath}/{Const.RegulationName}") ?? throw new InvalidOperationException());
        setBndFile(_regulationBnd, Const.CharaInitParamName, _charaInitParam.Write());
        setBndFile(_regulationBnd, Const.ItemLotParam_mapName, _itemLotParam_map.Write());
        setBndFile(_regulationBnd, Const.ItemLotParam_enemyName, _itemLotParam_enemy.Write());
        setBndFile(_regulationBnd, Const.ShopLineupParamName, _shopLineupParam.Write());
        setBndFile(_regulationBnd, Const.EquipParamWeaponName, _equipParamWeapon.Write());
        setBndFile(_regulationBnd, Const.AtkParamPcName, _atkParam_Pc.Write());
        SFUtil.EncryptERRegulation($"{Const.BingoPath}/{Const.RegulationName}", _regulationBnd);
        Directory.CreateDirectory(Path.GetDirectoryName($"{Const.BingoPath}/{Const.MenuMsgBNDPath}") ?? throw new InvalidOperationException());
        setBndFile(_menuMsgBND, Const.GR_LineHelpName, _lineHelpFmg.Write());
        File.WriteAllBytes($"{Const.BingoPath}/{Const.MenuMsgBNDPath}", _menuMsgBND.Write());
    }

    private string getRequiredLevelsWeapon(CharaInitParam chr, int id)
    {   // TODO reimplement to account for DLC gear
        return " :: ";

        EquipParamWeapon wep = _weaponDictionary[id]; // TODO dlc weapons not included
        int reqLevels = 0;
        if (wep.properStrength > chr.baseStr)
        {
            reqLevels += wep.properStrength - chr.baseStr;
        }
        if (wep.properAgility > chr.baseDex)
        {
            reqLevels += wep.properAgility - chr.baseDex;
        }
        if (wep.properMagic > chr.baseMag)
        {
            reqLevels += wep.properMagic - chr.baseMag;
        }
        if (wep.properFaith > chr.baseFai)
        {
            reqLevels += wep.properFaith - chr.baseFai;
        }
        if (wep.properLuck > chr.baseLuc)
        {
            reqLevels += wep.properLuck - chr.baseLuc;
        }
        return reqLevels > 0 ? $" (-{reqLevels})" : "";
    }
    private string getRequiredLevelsSpell(CharaInitParam chr, int id)
    {
        Magic spell = _magicDictionary[id];
        int reqLevels = 0;
        if (spell.requirementIntellect > chr.baseMag)
        {
            reqLevels += spell.requirementIntellect - chr.baseMag;
        }
        if (spell.requirementFaith > chr.baseFai)
        {
            reqLevels += spell.requirementFaith - chr.baseFai;
        }
        if (spell.requirementLuck > chr.baseLuc)
        {
            reqLevels += spell.requirementLuck - chr.baseLuc;
        }
        return reqLevels > 0 ? $" (-{reqLevels})" : "";
    }

    private static T getNewId<T>(int oldId, IList<T> queue) where T : IEquatable<int>
    {   // used to allocate shop items
        if (queue.All(i => i.Equals(oldId)))
        {
            Debug.WriteLine($"No New Ids for {oldId}");
            return queue.Pop();
        }

        T newId = queue.Pop();
        while (newId.Equals(oldId))
        {   // does not allow original weapon at shop slot
            queue.Insert(0, newId);
            newId = queue.Pop();
        }
        return newId;
    }
    // ReSharper disable once SuggestBaseTypeForParameter
    private static void addToOrderedDict<T>(IOrderedDictionary orderedDict, object key, T type)
    {
        List<T>? ids = (List<T>?)orderedDict[key];
        if (ids != null)
        { ids.Add(type); }
        else
        {
            ids = new List<T> { type, };
            orderedDict.Add(key, ids);
        }
    }
    private static bool chrCanUseWeapon(EquipParamWeapon wep, CharaInitParam chr)
    {
        return wep.properStrength <= chr.baseStr
            && wep.properAgility <= chr.baseDex
            && wep.properMagic <= chr.baseMag
            && wep.properFaith <= chr.baseFai
            && wep.properLuck <= chr.baseLuc;
    }
    private static bool chrCanUseSpell(Magic spell, CharaInitParam chr)
    {
        return spell.requirementIntellect <= chr.baseMag
            && spell.requirementFaith <= chr.baseFai
            && spell.requirementLuck <= chr.baseLuc;
    }
    private static void setBndFile(IBinder binder, string fileName, byte[] bytes)
    {
        BinderFile file = binder.Files.First(file => file.Name.EndsWith(fileName)) ?? throw new BinderFileNotFoundException(fileName);
        file.Bytes = bytes;
    }
    private static void patchSpEffectAtkPowerCorrectRate(AtkParam atkParam)
    {
        atkParam.spEffectAtkPowerCorrectRate_byPoint = 100;
        atkParam.spEffectAtkPowerCorrectRate_byRate = 100;
        atkParam.spEffectAtkPowerCorrectRate_byDmg = 100;
    }
    private static void copyShopLineupParam(ShopLineupParam lot, ShopLineupParam shopLineupParam)
    {
        lot.equipId = shopLineupParam.equipId;
        lot.costType = shopLineupParam.costType;
        lot.sellQuantity = shopLineupParam.sellQuantity;
        lot.setNum = shopLineupParam.setNum;
        lot.value = shopLineupParam.value;
        lot.value_Add = shopLineupParam.value_Add;
        lot.value_Magnification = shopLineupParam.value_Magnification;
        lot.iconId = shopLineupParam.iconId;
        lot.nameMsgId = shopLineupParam.nameMsgId;
        lot.menuIconId = shopLineupParam.menuIconId;
        lot.menuTitleMsgId = shopLineupParam.menuTitleMsgId;
    }
    private static int washWeaponMetadata(int id)
    {
        return id / 10000 * 10000;
    }
    private static int washWeaponLevels(int id)
    {
        return id / 100 * 100;
    }
}