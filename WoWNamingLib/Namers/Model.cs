﻿using DBCD;
using System.Diagnostics;
using WoWNamingLib.Services;

namespace WoWNamingLib.Namers
{
    public static class Model
    {
        private struct SpellEntry
        {
            public uint SpellID;
            public string SpellName;
            public uint EventStart;
        }

        private static uint fileDataID;
        private static string currentModelName;

        private static string GetCFDSuffixedName(DBCDRow cfdRow, Dictionary<uint, string> racePrefix, string modelName)
        {
            if (!racePrefix.TryGetValue(uint.Parse(cfdRow["RaceID"].ToString()), out string miniComponentRace))
            {
                Console.WriteLine("!!! Unknown race prefix for race ID " + cfdRow["RaceID"].ToString());
                miniComponentRace = "xx";
            }

            var miniComponentGender = uint.Parse(cfdRow["GenderIndex"].ToString()) switch
            {
                0 => "m",
                1 => "f",
                2 or 3 => "u",
                _ => throw new Exception("unknown component gender index " + cfdRow["GenderIndex"].ToString()),
            };

            if (cfdRow["PositionIndex"].ToString() == "-1")
            {
                return modelName + "_" + miniComponentRace + "_" + miniComponentGender;
            }
            else if (cfdRow["PositionIndex"].ToString() == "0")
            {
                return modelName + "_l";
            }
            else if (cfdRow["PositionIndex"].ToString() == "1")
            {
                return modelName + "_r";
            }
            else
            {
                return "";
            }
        }

        public static void Name(List<uint> fileDataIDs, bool forceFullRun = false)
        {
            var fullRun = forceFullRun;
            var m2s = fileDataIDs;

            if(m2s.Count == 0 || m2s.All(x => x == 0))
                fullRun = true;

            if (fullRun)
            {
                var mfd = Namer.LoadDBC("ModelFileData");
                foreach (var entry in mfd.Values)
                {
                    m2s.Add(uint.Parse(entry["FileDataID"].ToString()));
                }

                // Load model.blob
                using (var ms = new MemoryStream())
                {
                    try
                    {
                        var file = CASCManager.GetFileByID(764428).Result;
                        file.CopyTo(ms);
                        ms.Position = 0;

                        var bin = new BinaryReader(ms);

                        bin.BaseStream.Position += 16;
                        var size = bin.ReadUInt32();
                        var count = size / 28;
                        for (var i = 0; i < count; i++)
                        {
                            var m2fdid = bin.ReadUInt32();

                            if (!fullRun)
                            {
                                if (!m2s.Contains(m2fdid) && !Namer.IDToNameLookup.ContainsKey((int)m2fdid))
                                {
                                    Console.WriteLine("New M2 in model.blob: " + m2fdid);
                                    m2s.Add(m2fdid);
                                }
                            }
                            else
                            {
                                if (!m2s.Contains(m2fdid))
                                {
                                    Console.WriteLine("New M2 in model.blob: " + m2fdid);
                                    m2s.Add(m2fdid);
                                }
                            }

                            bin.BaseStream.Position += 24;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to read model.blob: " + e.Message);
                    }
                }

                // Cheat to do newer models first :>
                //m2s.Reverse();
            }

            var soundKitFDIDMap = new Dictionary<uint, List<uint>>();
            try
            {
                var soundKitDB = Namer.LoadDBC("SoundKitEntry");

                foreach (var soundKitEntry in soundKitDB.Values)
                {
                    var soundKitID = uint.Parse(soundKitEntry["SoundKitID"].ToString());
                    var soundKitFileDataID = uint.Parse(soundKitEntry["FileDataID"].ToString());
                    if (!soundKitFDIDMap.ContainsKey(soundKitID))
                    {
                        soundKitFDIDMap.Add(soundKitID, new List<uint>() { soundKitFileDataID });
                    }
                    else
                    {
                        soundKitFDIDMap[soundKitID].Add(soundKitFileDataID);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't load soundkit DB for model naming: " + e.Message);
            }

            var skyboxFDIDs = new List<uint>();
            try
            {
                var skyboxDB = Namer.LoadDBC("LightSkybox");

                foreach (var skyboxEntry in skyboxDB.Values)
                {
                    var skyboxFDID = uint.Parse(skyboxEntry["SkyboxFileDataID"].ToString());
                    skyboxFDIDs.Add(skyboxFDID);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't load skybox DB for model naming: " + e.Message);
            }

            var gameobjectFDIDs = new List<uint>();
            try
            {
                var godDB = Namer.LoadDBC("GameObjectDisplayInfo");

                foreach (var godEntry in godDB.Values)
                {
                    var goFDID = uint.Parse(godEntry["FileDataID"].ToString());
                    gameobjectFDIDs.Add(goFDID);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't load GameObjectDisplayInfo DB for model naming: " + e.Message);
            }

            var creatureFDIDs = new List<uint>();
            var cmdToFDID = new Dictionary<uint, uint>();
            var fdidToCreatureName = new Dictionary<uint, List<string>>();

            try
            {
                var cmdDB = Namer.LoadDBC("CreatureModelData");

                foreach (var cmdEntry in cmdDB.Values)
                {
                    var cmdFDID = uint.Parse(cmdEntry["FileDataID"].ToString());
                    var cmdID = uint.Parse(cmdEntry["ID"].ToString());
                    cmdToFDID.Add(cmdID, cmdFDID);
                    creatureFDIDs.Add(cmdFDID);
                }

                var creatureDisplayInfoDB = Namer.LoadDBC("CreatureDisplayInfo");
                foreach (var cdiEntry in creatureDisplayInfoDB.Values)
                {
                    var cdiID = uint.Parse(cdiEntry["ID"].ToString());
                    var modelID = uint.Parse(cdiEntry["ModelID"].ToString());
                    //if (Namer.displayIDToCreatureName.TryGetValue(cdiID, out var name))
                    //{
                    //    if (!fdidToCreatureName.ContainsKey(cmdToFDID[modelID]))
                    //    {
                    //        fdidToCreatureName.Add(cmdToFDID[modelID], name);
                    //    }
                    //}
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't load Creature DBs for model naming: " + e.Message);
            }

            var groundEffectDoodadFDIDs = new List<uint>();
            try
            {
                var gedDB = Namer.LoadDBC("GroundEffectDoodad");
                foreach (var gedEntry in gedDB.Values)
                {
                    var gedFDID = uint.Parse(gedEntry["ModelFileID"].ToString());
                    groundEffectDoodadFDIDs.Add(gedFDID);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Can't load GroundEffectDoodad DB for model naming: " + e.Message);
            }

            var spellFDIDs = new List<uint>();
            var spellNamesClean = new Dictionary<uint, string>();

            Console.WriteLine("Loading spell DBs");

            // SpellVisualKitModelAttach::SpellVisualEffectNameID   => SpellVisualKitID
            // SpellVisualKitEffect::Effect (EffectType == 2)       => SpellVisualKitID

            // SpellVisual::SpellVisualMissileSetID => SpellVisualMissile
            // SpellVisualMissile::SpellVisualEffectNameID

            // SpellVisualEvent::SpellVisualKitID => SpellVisualID
            // SpellXSpellVisual::SpellVisualID => SpellID

            var svenMap = new Dictionary<uint, uint>();
            if (fullRun)
            {
                try
                {
                    var spellNames = new Dictionary<uint, List<SpellEntry>>();

                    var svkmaDB = Namer.LoadDBC("SpellVisualKitModelAttach");
                    var svkeDB = Namer.LoadDBC("SpellVisualKitEffect");
                    var sveDB = Namer.LoadDBC("SpellVisualEvent");
                    var svenDB = Namer.LoadDBC("SpellVisualEffectName");
                    var svToSpellDB = Namer.LoadDBC("SpellXSpellVisual");

                    var svenToKit = new Dictionary<uint, List<uint>>();
                    var svkmaIDToSvenID = new Dictionary<int, uint>();

                    foreach (var svkmaEntry in svkmaDB.Values)
                    {
                        var svenID = uint.Parse(svkmaEntry["SpellVisualEffectNameID"].ToString());
                        var svkID = uint.Parse(svkmaEntry["ParentSpellVisualKitID"].ToString());

                        svkmaIDToSvenID.Add(svkmaEntry.ID, svenID);

                        if (!svenToKit.ContainsKey(svenID))
                        {
                            svenToKit.Add(svenID, new List<uint>() { svkID });
                        }
                        else
                        {
                            if (!svenToKit[svenID].Contains(svkID))
                                svenToKit[svenID].Add(svkID);
                        }
                    }

                    foreach (var svkeEntry in svkeDB.Values)
                    {
                        if (uint.Parse(svkeEntry["EffectType"].ToString()) != 2)
                            continue;

                        var spkmaID = uint.Parse(svkeEntry["Effect"].ToString());
                        var svkID = uint.Parse(svkeEntry["ParentSpellVisualKitID"].ToString());

                        if (svkmaIDToSvenID.TryGetValue((int)spkmaID, out var svenID))
                        {
                            if (!svenToKit.ContainsKey(svenID))
                            {
                                svenToKit.Add(svenID, new List<uint>() { svkID });
                            }
                            else
                            {
                                if (!svenToKit[svenID].Contains(svkID))
                                    svenToKit[svenID].Add(svkID);
                            }
                        }
                    }

                    var svkToType = new Dictionary<uint, uint>();
                    var kitToVisual = new Dictionary<uint, List<uint>>();
                    foreach (var sveEntry in sveDB.Values)
                    {
                        var svkID = uint.Parse(sveEntry["SpellVisualKitID"].ToString());
                        var svID = uint.Parse(sveEntry["SpellVisualID"].ToString());

                        if (!svkToType.ContainsKey(svkID))
                            svkToType.Add(svkID, uint.Parse(sveEntry["StartEvent"].ToString()));

                        if (svID == 0)
                            continue;

                        if (!kitToVisual.ContainsKey(svkID))
                            kitToVisual.Add(svkID, new List<uint>() { svID });
                        else
                            kitToVisual[svkID].Add(svID);
                    }

                    var spellVisualToSpell = new Dictionary<uint, List<uint>>();
                    foreach (var svToSpellEntry in svToSpellDB.Values)
                    {
                        var svID = uint.Parse(svToSpellEntry["SpellVisualID"].ToString());
                        var spellID = uint.Parse(svToSpellEntry["SpellID"].ToString());

                        if (!spellVisualToSpell.ContainsKey(svID))
                            spellVisualToSpell.Add(svID, new List<uint>() { spellID });
                        else
                            spellVisualToSpell[svID].Add(spellID);
                    }

                    var spellNameDB = Namer.LoadDBC("SpellName");
                    var spellToSpellName = new Dictionary<uint, string>();
                    foreach (var spellNameEntry in spellNameDB.Values)
                    {
                        var spellID = uint.Parse(spellNameEntry["ID"].ToString());
                        var spellName = spellNameEntry["Name_lang"].ToString();
                        spellToSpellName.Add(spellID, spellName);
                    }

                    foreach (var svenEntry in svenDB.Values)
                    {
                        var svenFDID = uint.Parse(svenEntry["ModelFileDataID"].ToString());
                        if (svenFDID == 0)
                            continue;

                        if (uint.Parse(svenEntry["Type"].ToString()) != 0)
                            continue;

                        if (Namer.placeholderNames.Contains((int)svenFDID) || !Namer.IDToNameLookup.ContainsKey((int)svenFDID))
                        {
                            svenMap.Add(uint.Parse(svenEntry["ID"].ToString()), svenFDID);
                            spellFDIDs.Add(svenFDID);
                        }
                    }

                    var spellOutputLines = new List<string>();

                    foreach (var svenEntry in svenMap)
                    {
                        var svenID = svenEntry.Key;
                        var spellModelFDID = svenEntry.Value;

                        if (!svenToKit.TryGetValue(svenID, out var svkIDs))
                            continue;

                        spellOutputLines.Add(spellModelFDID + " (SpellVisualEffectName ID " + svenID + ")");
                        foreach (var svkID in svkIDs)
                        {
                            spellOutputLines.Add("\t SpellKitVisualID " + svkID);

                            if (!kitToVisual.TryGetValue(svkID, out var svIDs))
                                continue;

                            foreach (var svID in svIDs)
                            {
                                spellOutputLines.Add("\t\t SpellVisualID " + svID);

                                if (!spellVisualToSpell.TryGetValue(svID, out var spellIDs))
                                    continue;

                                foreach (var spellID in spellIDs)
                                {
                                    if (!spellToSpellName.TryGetValue(spellID, out var spellName))
                                        continue;

                                    spellOutputLines.Add("\t\t\t " + spellName + " (SpellID " + spellID + ")");

                                    var eventStart = svkToType[svkID];
                                    if (!spellNames.ContainsKey(spellModelFDID))
                                        spellNames.Add(spellModelFDID, new List<SpellEntry>() { new SpellEntry() { SpellID = spellID, SpellName = spellName, EventStart = eventStart } });
                                    else
                                        spellNames[spellModelFDID].Add(new SpellEntry() { SpellID = spellID, SpellName = spellName, EventStart = eventStart });
                                }
                            }
                        }
                    }

                    var spellClassOptionDB = Namer.LoadDBC("SpellClassOptions");
                    var classSpells = new List<uint>();
                    foreach (var scoEntry in spellClassOptionDB.Values)
                    {
                        var spellID = uint.Parse(scoEntry["SpellID"].ToString());
                        if (!classSpells.Contains(spellID))
                            classSpells.Add(spellID);
                    }

                    var journalEncounterSectionDB = Namer.LoadDBC("JournalEncounterSection");
                    var encounterSpells = new List<uint>();
                    foreach (var jesEntry in journalEncounterSectionDB.Values)
                    {
                        var spellID = uint.Parse(jesEntry["SpellID"].ToString());
                        if (!encounterSpells.Contains(spellID))
                            encounterSpells.Add(spellID);
                    }

                    var spellModelNames = new Dictionary<uint, SpellEntry>();

                    foreach (var spellNameEntry in spellNames)
                    {
                        var spellModelFDID = spellNameEntry.Key;
                        var spellNameList = spellNameEntry.Value;
                        var spellName = new SpellEntry();
                        if (spellNameList.Count == 1)
                            spellName = spellNameList[0];
                        else
                        {

                            var spellNameDict = new Dictionary<string, int>();
                            foreach (var name in spellNameList)
                            {
                                if (!spellNameDict.ContainsKey(name.SpellName))
                                    spellNameDict.Add(name.SpellName, 1);
                                else
                                    spellNameDict[name.SpellName]++;

                                if (encounterSpells.Contains(name.SpellID))
                                    spellNameDict[name.SpellName] = 99;

                                if (classSpells.Contains(name.SpellID))
                                    spellNameDict[name.SpellName] = 99;
                            }
                            var maxCount = 0;
                            foreach (var name in spellNameDict)
                            {
                                if (name.Value > maxCount)
                                {
                                    maxCount = name.Value;
                                    spellName = spellNameList.Where(x => x.SpellName == name.Key).First();
                                }
                            }
                        }

                        if (spellName.SpellName == null)
                        {
                            Console.WriteLine("No name found for " + spellModelFDID);
                            continue;
                        }

                        if (!spellModelNames.ContainsKey(spellModelFDID))
                            spellModelNames.Add(spellModelFDID, spellName);
                    }

                    spellOutputLines.Add("## __ CALCULATED NAMES __ ##");

                    var journalInstanceDB = Namer.LoadDBC("JournalInstance");
                    var journalEncounterDB = Namer.LoadDBC("JournalEncounter");

                    foreach (var spellModelName in spellModelNames)
                    {
                        var spellname = spellModelName.Value.SpellName;
                        var spellID = spellModelName.Value.SpellID;
                        var prefix = "";

                        if (encounterSpells.Contains(spellID))
                        {
                            prefix = "10FX_";

                            // Figure out instance?
                            foreach (var jesEntry in journalEncounterSectionDB.Values)
                            {
                                var jesSpellID = uint.Parse(jesEntry["SpellID"].ToString());
                                if (jesSpellID != spellID)
                                    continue;

                                var jesEncounterID = uint.Parse(jesEntry["JournalEncounterID"].ToString());
                                foreach (var jeEntry in journalEncounterDB.Values)
                                {
                                    if (jeEntry.ID != jesEncounterID)
                                        continue;

                                    var jeInstanceID = uint.Parse(jeEntry["JournalInstanceID"].ToString());
                                    foreach (var jiEntry in journalInstanceDB.Values)
                                    {
                                        if (jiEntry.ID != jeInstanceID)
                                            continue;

                                        var jiName = jiEntry["Name_lang"].ToString();
                                        var jiNameClean = jiName.Replace(" ", "").Replace("'", "").Replace(",", "").Replace("-", "").Replace(":", "");
                                        switch (jiNameClean)
                                        {
                                            case "TheVortexPinnacle":
                                                jiNameClean = "VortexPinnacle";
                                                break;
                                            case "TheNokhudOffensive":
                                                jiNameClean = "Nokhud";
                                                break;
                                            case "TheAzureVault":
                                                jiNameClean = "AzureVault";
                                                break;
                                            case "AberrustheShadowedCrucible":
                                                jiNameClean = "Aberrus";
                                                break;
                                            case "UldamanLegacyOfTyr":
                                                jiNameClean = "UldamanLOT";
                                                break;
                                            case "AlgetharAcademy":
                                                jiNameClean = "Algethar";
                                                break;
                                            case "DawnoftheInfinite":
                                                jiNameClean = "BronzeDungeon";
                                                break;
                                            case "VaultoftheIncarnates":
                                                jiNameClean = "VOTI";
                                                break;
                                            case "AmirdrassiltheDreamsHope":
                                                jiNameClean = "Amirdrassil";
                                                break;
                                            case "SepulcheroftheFirstOnes":
                                                jiNameClean = "Sepulcher";
                                                prefix = "9FX_";
                                                break;
                                        }

                                        var jeName = jeEntry["Name_lang"].ToString();
                                        var jeNameClean = jeName.Replace(" ", "").Replace("'", "").Replace(",", "").Replace("-", "").Replace(":", "");
                                        switch (jeNameClean)
                                        {
                                            case "ScalecommanderSarkareth":
                                                jeNameClean = "Sarkareth";
                                                break;
                                            case "TheVigilantStewardZskarn":
                                                jeNameClean = "Zskarn";
                                                break;
                                            case "TheAmalgamationChamber":
                                                jeNameClean = "Amalgamation";
                                                break;
                                            case "TheForgottenExperiments":
                                                jeNameClean = "ForgottenExperiments";
                                                break;
                                            case "KazzaratheHellforged":
                                                jeNameClean = "Kazzara";
                                                break;
                                            case "RashoktheElder":
                                                jeNameClean = "Rashok";
                                                break;
                                            case "AssaultoftheZaqali":
                                                jeNameClean = "Zaqali";
                                                break;
                                            case "RaszageththeStormEater":
                                                jeNameClean = "Raszageth";
                                                break;
                                            case "DatheaAscended":
                                                jeNameClean = "Dathea";
                                                break;
                                            case "KurogGrimtotem":
                                                jeNameClean = "Kurog";
                                                break;
                                            case "LiskanothTheFuturebane":
                                                jeNameClean = "Liskanoth";
                                                break;
                                            case "ForgemasterGorek":
                                                jeNameClean = "Gorek";
                                                break;
                                            case "MelidrussaChillworn":
                                                jeNameClean = "Melidrussa";
                                                break;
                                            case "IridikrontheStonescaled":
                                                jeNameClean = "Iridikron";
                                                break;
                                            case "ChronoLordDeios":
                                                jeNameClean = "Deios";
                                                break;
                                            case "ManifestedTimeways":
                                                jeNameClean = "Timeways";
                                                break;
                                            case "TyrtheInfiniteKeeper":
                                                jeNameClean = "Tyr";
                                                break;
                                            case "SennarththeColdBreath":
                                                jeNameClean = "Sennarth";
                                                break;
                                            case "FyrakktheBlazing":
                                                jeNameClean = "Fyrakk";
                                                break;
                                        }

                                        prefix += jiNameClean + "_" + jeNameClean + "_";
                                        break;
                                    }

                                    break;
                                }
                                break;
                            }
                        }
                        else if (classSpells.Contains(spellID))
                        {
                            prefix = "CFX_";
                            foreach (var scoEntry in spellClassOptionDB.Values)
                            {
                                var scoSpellID = uint.Parse(scoEntry["SpellID"].ToString());
                                if (scoSpellID != spellID)
                                    continue;

                                var classSet = uint.Parse(scoEntry["SpellClassSet"].ToString());

                                switch (classSet)
                                {
                                    case 3:
                                        prefix += "Mage_";
                                        break;
                                    case 4:
                                        prefix += "Warrior_";
                                        break;
                                    case 5:
                                        prefix += "Warlock_";
                                        break;
                                    case 6:
                                        prefix += "Priest_";
                                        break;
                                    case 7:
                                        prefix += "Druid_";
                                        break;
                                    case 8:
                                        prefix += "Rogue_";
                                        break;
                                    case 9:
                                        prefix += "Hunter_";
                                        break;
                                    case 10:
                                        prefix += "Paladin_";
                                        break;
                                    case 11:
                                        prefix += "Shaman_";
                                        break;
                                    case 15:
                                        prefix += "DeathKnight_";
                                        break;
                                    case 53:
                                        prefix += "Monk_";
                                        break;
                                    case 107:
                                        prefix += "DemonHunter_";
                                        break;
                                    case 224:
                                        prefix += "Evoker_";
                                        break;
                                }

                                break;
                            }
                        }
                        else
                        {
                            prefix = "FX_";
                        }

                        var eventSuffix = "";

                        switch (spellModelName.Value.EventStart)
                        {
                            case 1:
                                eventSuffix = "_Precast";
                                break;
                            case 2:
                                eventSuffix = "_Precast";
                                break;
                            case 3:
                                eventSuffix = "_Cast";
                                break;
                            case 4:
                                eventSuffix = "_Travel";
                                break;
                            case 5:
                                eventSuffix = "_TravelE";
                                break;
                            case 6:
                                eventSuffix = "_Impact";
                                break;
                            case 7:
                                eventSuffix = "_Aura";
                                break;
                            case 8:
                                eventSuffix = "_Aura";
                                break;
                            case 9:
                                eventSuffix = "_AreaTrigger";
                                break;
                            case 11:
                                eventSuffix = "_Channel";
                                break;
                            case 12:
                                eventSuffix = "_Channel";
                                break;
                            case 13:
                                eventSuffix = "_OneShot";
                                break;
                            default:
                                Console.WriteLine("Unknown event start " + spellModelName.Value.EventStart + " for Spell " + spellID + " (" + spellModelName.Value.SpellName + ")");
                                break;

                        }

                        var cleanSpellname = spellname.Replace(" ", "").Replace("'", "").Replace("-", "").Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Replace(":", "").Replace(";", "").Replace("DNT", "").Replace("+", "").Replace("<", "").Replace(">", "").Replace("!", "");
                        var calculatedName = "spells/" + prefix + cleanSpellname + eventSuffix + ".m2";
                        var nameSaved = false;
                        var numIndex = 0;

                        while (!nameSaved)
                        {
                            if (!spellNamesClean.ContainsValue(calculatedName) && !Namer.IDToNameLookup.Values.Contains(calculatedName))
                            {
                                spellNamesClean.Add(spellModelName.Key, calculatedName);
                                nameSaved = true;
                            }
                            else
                            {
                                numIndex++;
                                calculatedName = "spells/" + prefix + cleanSpellname + eventSuffix + numIndex.ToString().PadLeft(2, '0') + ".m2";
                            }
                        }

                        spellOutputLines.Add(spellModelName.Key + ": " + spellModelName.Value.SpellName + "(" + spellModelName.Value.SpellID + ") = " + calculatedName);
                    }

                    File.WriteAllLines("spellOutput.txt", spellOutputLines.ToArray());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Can't load Spell DBs for model naming: " + e.Message);
                }
            }

            var itemAppearance = Namer.LoadDBC("ItemAppearance");
            var itemModelNames = new Dictionary<uint, string>();

            var itemFDIDs = new List<uint>();

            if (fullRun)
            {
                try
                {
                    var mfdDict = new Dictionary<uint, List<uint>>();

                    var mfdDB = Namer.LoadDBC("ModelFileData");
                    foreach (var mfdEntry in mfdDB.Values)
                    {
                        var mfdID = uint.Parse(mfdEntry["ModelResourcesID"].ToString());
                        var mfdFDID = uint.Parse(mfdEntry["FileDataID"].ToString());
                        if (!mfdDict.ContainsKey(mfdID))
                        {
                            mfdDict.Add(mfdID, new List<uint>() { mfdFDID });
                        }
                        else
                        {
                            mfdDict[mfdID].Add(mfdFDID);
                        }
                    }


                    var cfdDB = Namer.LoadDBC("ComponentModelFileData");
                    var idiDB = Namer.LoadDBC("ItemDisplayInfo");
                    var chrRaces = Namer.LoadDBC("ChrRaces");
                    var racePrefix = new Dictionary<uint, string>();
                    foreach (dynamic chrRaceEntry in chrRaces.Values)
                    {
                        racePrefix.Add(uint.Parse(chrRaceEntry.ID.ToString()), chrRaceEntry.ClientPrefix.ToString());
                    }

                    foreach (var cfdEntry in cfdDB.Values)
                    {
                        var cfdFDID = uint.Parse(cfdEntry["ID"].ToString());
                        if (!itemFDIDs.Contains(cfdFDID))
                            itemFDIDs.Add(cfdFDID);
                    }

                    foreach (var idiEntry in idiDB.Values)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            var modelRes = ((uint[])idiEntry["ModelResourcesID"])[i];
                            if (mfdDict.TryGetValue(modelRes, out var itemM2FDIDs))
                            {
                                itemFDIDs.AddRange(itemM2FDIDs);

                                foreach (var itemM2FDID in itemM2FDIDs)
                                {
                                    if (itemM2FDID == 0)
                                        continue;

                                    if (itemModelNames.ContainsKey(itemM2FDID))
                                        continue;

                                    var itemModelIsNamed = true;

                                    if (Namer.IDToNameLookup.TryGetValue((int)itemM2FDID, out var currentItemFilename))
                                    {
                                        if (Namer.placeholderNames.Contains((int)itemM2FDID))
                                        {
                                            // Is unnamed
                                            itemModelIsNamed = false;
                                        }
                                    }
                                    else
                                    {
                                        // Is unnamed
                                        itemModelIsNamed = false;
                                    }

                                    if (!itemModelIsNamed)
                                    {
                                        uint iconFDID = 0;

                                        foreach (dynamic iaRow in itemAppearance.Values)
                                        {
                                            if (uint.Parse(iaRow["ItemDisplayInfoID"].ToString()) == uint.Parse(idiEntry.ID.ToString()))
                                            {
                                                iconFDID = uint.Parse(iaRow.DefaultIconFileDataID.ToString());
                                            }
                                        }

                                        if (iconFDID != 0 && iconFDID != 136235 && Namer.IDToNameLookup.TryGetValue((int)iconFDID, out string iconFileName))
                                        {
                                            var cleanedName = iconFileName.ToLower().Replace("\\", "/").Replace("interface/icons/inv_", "").Replace("interface/icons/", "").Replace(".blp", "").Trim();

                                            if (iconFileName.ToLower().Contains("questionmark") || iconFileName.ToLower() == "interface/icons/temp.blp")
                                                continue;

                                            Console.WriteLine("!!! Unnamed item M2 " + itemM2FDID + " has an icon with name " + cleanedName);
                                            if (cfdDB.TryGetValue(int.Parse(itemM2FDID.ToString()), out var cfdRow))
                                            {
                                                // Console.WriteLine(itemM2FDID.ToString() + " is for race " + cfdRow["RaceID"].ToString() + " " + cfdRow["GenderIndex"].ToString() + " sex " + cfdRow["GenderIndex"].ToString()  + " pos " + cfdRow["PositionIndex"].ToString());

                                                var cfdSuffixedName = GetCFDSuffixedName(cfdRow, racePrefix, cleanedName);
                                                if (cfdSuffixedName != "")
                                                {
                                                    itemModelNames.Add(itemM2FDID, cfdSuffixedName);
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Got empty cfd suffix for " + itemM2FDID + " " + cleanedName);
                                                }
                                            }
                                            else
                                            {
                                                itemModelNames.Add(itemM2FDID, cleanedName);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    foreach (var itemFDID in itemFDIDs)
                    {
                        var itemModelIsNamed = true;

                        if (Namer.IDToNameLookup.TryGetValue((int)itemFDID, out var currentItemFilename))
                        {
                            if (Namer.placeholderNames.Contains((int)itemFDID))
                            {
                                // Is unnamed
                                itemModelIsNamed = false;
                            }
                        }
                        else
                        {
                            // Is unnamed
                            itemModelIsNamed = false;
                        }

                        if (itemModelIsNamed || itemModelNames.ContainsKey(itemFDID))
                            continue;

                        if (cfdDB.TryGetValue(int.Parse(itemFDID.ToString()), out var cfdRow))
                        {
                            var cleanedName = itemFDID.ToString();
                            var cfdSuffixedName = GetCFDSuffixedName(cfdRow, racePrefix, cleanedName);
                            if (cfdSuffixedName != "")
                            {
                                itemModelNames.Add(itemFDID, cfdSuffixedName);
                            }
                            else
                            {
                                Console.WriteLine("Got empty cfd suffix for " + itemFDID);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Can't load item data for model naming: " + e.Message + " " + e.StackTrace);
                }
            }

            var unkPrefixes = new Dictionary<string, int>();

            var m2Counter = 0;
            foreach (var fdid in m2s)
            {
                fileDataID = fdid;
                var encrypted = false;

                if (m2Counter % 4000 == 0)
                    Console.WriteLine("Processed " + m2Counter + "/" + m2s.Count + " M2s");

                m2Counter++;

                using (var ms = new MemoryStream())
                {
                    try
                    {
                        var file = CASCManager.GetFileByID(fdid).Result;
                        file.CopyTo(ms);
                        ms.Position = 0;

                        var bin = new BinaryReader(ms);
                        if (bin.ReadUInt64() == 0)
                            encrypted = true;

                        ms.Position = 0;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        continue;
                    }

                    M2Model m2 = new M2Model();
                    var overrideName = false;
                    var forceOverrideName = false;

                    if (Namer.ForceRename.Contains(fdid))
                    {
                        overrideName = true;
                        forceOverrideName = true;
                    }

                    try
                    {
                        if (!encrypted)
                            m2 = ParseM2(ms);

                        if (encrypted == false && m2.name != null && !string.IsNullOrWhiteSpace(m2.name) && !int.TryParse(m2.name, out _))
                        {
                            currentModelName = m2.name;
                        }
                        else if (Namer.IDToNameLookup.TryGetValue((int)fdid, out string existingName) && !Namer.placeholderNames.Contains((int)fdid))
                        {
                            currentModelName = Path.GetFileNameWithoutExtension(existingName);
                        }
                        else if (itemModelNames.TryGetValue(fdid, out currentModelName))
                        {
                            Console.WriteLine("Found item name based on icons: " + currentModelName);
                        }
                        else if (spellNamesClean.TryGetValue(fdid, out currentModelName))
                        {
                            currentModelName = Path.GetFileNameWithoutExtension(currentModelName);
                            Console.WriteLine("Found spell name: " + currentModelName);
                        }
                        //else if (Namer.ForceRename.Contains(fdid) && Namer.IDToNameLookup.TryGetValue(fileDataID, out existingName))
                        //{
                        //    currentModelName = Path.GetFileNameWithoutExtension(existingName);
                        //}
                        else if (fdidToCreatureName.TryGetValue(fdid, out var creatureNames))
                        {
                            Console.WriteLine("Found creature names " + string.Join(", ", creatureNames) + " for FDID " + fdid);
                            currentModelName = fileDataID.ToString();
                        }
                        else
                        {
                            currentModelName = fileDataID.ToString();
                        }

                        var folder = "";

                        if (
                            !Namer.IDToNameLookup.ContainsKey((int)fdid) || (m2.name != null && m2.name.ToLower() != Path.GetFileNameWithoutExtension(Namer.IDToNameLookup[(int)fdid]).ToLower() || Namer.placeholderNames.Contains((int)fdid))
                            )
                        {
                            // Time to figure out a name!

                            if (Namer.placeholderNames.Contains((int)fdid))
                                overrideName = true;

                            string[] splitModelName;

                            if (encrypted == false && m2.name != null && !int.TryParse(m2.name, out _))
                            {
                                splitModelName = m2.name.ToLower().Split('_');
                            }
                            else
                            {
                                splitModelName = currentModelName.ToLower().Split('_');
                            }

                            switch (splitModelName[0])
                            {
                                // DOODADS
                                // 10.0
                                case "10xp":
                                    folder = "world/expansion09/doodads";
                                    break;
                                case "10hgl":
                                    folder = "world/expansion09/doodads/highlands";
                                    break;
                                case "10pm":
                                    folder = "world/expansion09/doodads/primalist";
                                    break;
                                case "10dg":
                                    folder = "world/expansion09/doodads/dragon";
                                    break;
                                case "10gl":
                                    folder = "world/expansion09/doodads/gnoll";
                                    break;
                                case "10fx":
                                    folder = "spells";
                                    break;
                                case "10gsl":
                                    folder = "world/expansion09/doodads/grasslands";
                                    break;
                                case "10du":
                                    folder = "world/expansion09/doodads/dungeon";
                                    break;
                                case "10ts":
                                    folder = "world/expansion09/doodads/tuskarr";
                                    break;
                                case "10can":
                                    folder = "world/expansion09/doodads/volcanic";
                                    break;
                                case "10dj":
                                    folder = "world/expansion09/doodads/djaradin";
                                    break;
                                case "10rq":
                                    folder = "world/expansion09/doodads/reliquary";
                                    break;
                                case "10el":
                                    folder = "world/expansion09/doodads/explorersleague";
                                    break;
                                case "10ti":
                                    folder = "world/expansion09/doodads/titan";
                                    break;
                                case "10ct":
                                    folder = "world/expansion09/doodads/centaur";
                                    break;
                                case "10gr":
                                    folder = "world/expansion09/doodads/gorloc";
                                    break;
                                case "10be":
                                    folder = "world/expansion09/doodads/bloodelf";
                                    break;
                                case "10ne":
                                    folder = "world/expansion09/doodads/nightelf";
                                    break;
                                case "10sp":
                                    folder = "world/expansion09/doodads/spider";
                                    break;
                                case "10mp":
                                    folder = "world/expansion09/doodads/molepeople";
                                    break;
                                case "10ed":
                                    folder = "world/expansion09/doodads/emeralddream";
                                    break;
                                //10dks

                                // 9.0
                                case "9fx":
                                    folder = "world/expansion08/doodads/fx";
                                    break;
                                case "9xp":
                                    folder = "world/expansion08/doodads";
                                    break;
                                case "9mal":
                                    folder = "world/expansion08/doodads/maldraxxus";
                                    break;
                                case "9cas":
                                    folder = "world/expansion08/doodads/castlezone";
                                    break;
                                case "9mw":
                                case "9maw":
                                    folder = "world/expansion08/doodads/maw";
                                    break;
                                case "9du":
                                    folder = "world/expansion08/doodads/dungeon";
                                    break;
                                case "9bo":
                                    folder = "world/expansion08/doodads/broker";
                                    break;
                                case "9mxt":
                                    folder = "world/expansion08/doodads/korthia";
                                    break;
                                case "9nc":
                                    folder = "world/expansion08/doodads/necro";
                                    break;
                                case "9hu":
                                    folder = "world/expansion08/doodads/human";
                                    break;
                                case "9ob":
                                case "9ori":
                                    folder = "world/expansion08/doodads/oribos";
                                    break;
                                case "9pln":
                                    folder = "world/expansion08/doodads/babylonzone";
                                    break;
                                case "9vm":
                                    folder = "world/expansion08/doodads/vampire";
                                    break;
                                case "9vl":
                                    folder = "world/expansion08/doodads/valkyr";
                                    break;
                                case "9prg":
                                case "9pg":
                                    folder = "world/expansion08/doodads/progenitor";
                                    break;
                                case "9ard":
                                    folder = "world/expansion08/doodads/ardenweald";
                                    break;
                                case "9fa":
                                    folder = "world/expansion08/doodads/fae";
                                    break;

                                // 8.0
                                case "8fx":
                                    folder = "world/expansion07/doodads/fx";
                                    break;
                                case "8riv":
                                    folder = "world/expansion07/doodads/riverzone";
                                    break;
                                case "8xp":
                                    folder = "world/expansion07/doodads";
                                    break;
                                case "8bl":
                                    folder = "world/expansion07/doodads/blackempire";
                                    break;
                                case "8nzo":
                                    folder = "world/expansion07/doodads/nzothraid";
                                    break;
                                case "8tr":
                                    // TODO
                                    break;

                                // 7.0
                                case "7vr":
                                    folder = "world/expansion06/doodads/vrykul";
                                    break;
                                case "7ne":
                                case "7nb":
                                case "7du":
                                case "7fx":
                                case "7hm":
                                case "7sr":
                                case "7dl":
                                case "7bs":
                                case "7lg":
                                case "7fk":
                                case "7af":
                                case "7sw":
                                case "7xp":
                                    // TODO
                                    break;

                                // 6.0
                                case "6fx":
                                    folder = "world/expansion05/doodads/fx";
                                    break;
                                case "6ar":
                                case "6hu":
                                case "6tj":
                                case "6dr":
                                case "6du":
                                case "6ak":
                                case "6oc":
                                case "6or":
                                    // TODO
                                    break;

                                // GOOBERS
                                case "g":
                                    folder = "world/goober/";
                                    break;

                                // SPELLS
                                case "fx":
                                case "cfx":
                                    folder = "spells";
                                    break;

                                // UI
                                case "ui":
                                    folder = "interface/glues/models";
                                    break;

                                // ITEMS
                                case "armor":
                                case "mail":
                                case "cloth":
                                case "leather":
                                case "chest":
                                case "pant":
                                case "plate":
                                case "belt":
                                case "robe":
                                case "feet":
                                case "boot":
                                case "crown":
                                case "hand":
                                case "quiver":
                                case "collections":
                                    folder = "item/objectcomponents/collections";
                                    break;
                                case "cape":
                                    folder = "item/objectcomponents/cape";
                                    break;
                                case "helm":
                                case "helmet":
                                    folder = "item/objectcomponents/head";
                                    break;
                                case "lshoulder":
                                case "rshoulder":
                                case "shoulder":
                                    folder = "item/objectcomponents/shoulder";
                                    break;
                                case "buckler":
                                case "shield":
                                    folder = "item/objectcomponents/shield";
                                    break;
                                case "buckle":
                                    folder = "item/objectcomponents/waist";
                                    break;
                                case "axe":
                                case "bow":
                                case "crossbow":
                                case "glaive":
                                case "firearm":
                                case "knife":
                                case "mace":
                                case "polearm":
                                case "offhand":
                                case "stave":
                                case "staff":
                                case "sword":
                                case "thrown":
                                case "hammer":
                                case "spear":
                                case "wand":
                                case "misc":
                                case "enchanting":
                                    folder = "item/objectcomponents/weapon";
                                    break;
                                case "arrow":
                                case "bullet":
                                    folder = "item/objectcomponents/ammo";
                                    break;
                                default:
                                    if (!unkPrefixes.ContainsKey(splitModelName[0]))
                                    {
                                        unkPrefixes.Add(splitModelName[0], 1);
                                    }
                                    else
                                    {
                                        unkPrefixes[splitModelName[0]]++;
                                    }
                                    if (!int.TryParse(splitModelName[0], out _))
                                    {
                                        Console.WriteLine("Unknown prefix: " + splitModelName[0] + " for name " + m2.name);
                                    }
                                    break;
                            }

                            // Override to nodxt/detail if ground effect doodad
                            if (groundEffectDoodadFDIDs.Contains(fdid))
                                folder = "models/world/nodxt/detail";

                            // No prefix found, so a creature?
                            if (folder == "" || folder.StartsWith("models"))
                            {
                                if (creatureFDIDs.Contains(fdid))
                                {
                                    if (currentModelName.All(char.IsDigit))
                                    {
                                        folder = "models/creature/unk_exp09_" + currentModelName;
                                    }
                                    else
                                    {
                                        folder = "models/creature/" + currentModelName;
                                    }
                                }
                                else if (groundEffectDoodadFDIDs.Contains(fdid))
                                {
                                    folder = "models/world/nodxt/detail";
                                }
                                else if (gameobjectFDIDs.Contains(fdid))
                                {
                                    if (currentModelName.All(char.IsDigit))
                                    {
                                        folder = "models/world/unk_exp09_" + currentModelName.ToLower();
                                    }
                                    else
                                    {
                                        folder = "models/world/" + currentModelName.ToLower();
                                    }
                                }
                                else if (itemFDIDs.Contains(fdid))
                                {
                                    folder = "models/item/unk_exp09_" + currentModelName.ToLower();
                                }
                                else if (spellFDIDs.Contains(fdid))
                                {
                                    if (spellNamesClean.ContainsKey(fdid))
                                    {

                                    }
                                    else
                                    {
                                        folder = "models/spells/unk_exp09_" + currentModelName.ToLower();
                                    }
                                }
                                else
                                {
                                    folder = "models/unknown/unk_exp09_" + currentModelName.ToLower();
                                }
                            }
                        }
                        else
                        {
                            folder = Path.GetDirectoryName(Namer.IDToNameLookup[(int)fdid]).Replace("\\", "/");
                        }

                        //if (Namer.ForceRename.Contains(fdid) && Namer.IDToNameLookup.TryGetValue(fileDataID, out var existingFolder))
                        //{
                        //    folder = Path.GetDirectoryName(existingFolder);
                        //}

                        if ((fdid != 395900 && fdid != 527723) && skyboxFDIDs.Contains(fdid) && folder.ToLower() != "environments/stars")
                        {
                            Console.WriteLine("Encountered skybox " + fdid + " in folder " + folder + ", forcing rename");
                            folder = "models/environments/stars";
                            overrideName = true;
                        }

                        if (currentModelName == "SpellVisualPlaceholder")
                        {
                            Console.WriteLine("Encountered SpellVisualPlaceholder " + fdid + " in folder " + folder + ", forcing rename");
                            currentModelName = "SpellVisualPlaceholder_" + fdid;
                            folder = "models/spells/svp_" + fdid;
                            //forceOverrideName = true;
                            //overrideName = true;
                        }

                        if (currentModelName == "7XP_Waterfall_Top")
                        {
                            Console.WriteLine("Encountered 7XP_Waterfall_Top " + fdid + " in folder " + folder + ", forcing rename");
                            currentModelName = "7XP_Waterfall_Top_" + fdid;
                            //forceOverrideName = true;
                            //overrideName = true;
                        }

                        if (overrideCheck(overrideName, fdid, forceOverrideName))
                            NewFileManager.AddNewFile(fdid, folder + "/" + currentModelName + ".m2", overrideCheck(overrideName, fdid, forceOverrideName), forceOverrideName);

                        if (encrypted)
                            continue;

                        if (overrideCheck(overrideName, m2.physFileID, forceOverrideName))
                            NewFileManager.AddNewFile(m2.physFileID, folder + "/" + currentModelName + ".phys", overrideCheck(overrideName, m2.physFileID, forceOverrideName), forceOverrideName);

                        if (overrideCheck(overrideName, m2.skelFileID, forceOverrideName))
                            NewFileManager.AddNewFile(m2.skelFileID, folder + "/" + currentModelName + ".skel", overrideCheck(overrideName, m2.skelFileID, forceOverrideName), forceOverrideName);

                        if (m2.skelFileID != 0)
                            Skel.Name(m2.skelFileID, currentModelName, folder, overrideName);

                        if (currentModelName == "7XP_Waterfall_Top")
                            Debugger.Break();

                        for (var i = 0; i < m2.skinFileDataIDs.Length; i++)
                        {
                            if (i > (m2.nViews - 1))
                            {
                                if (overrideCheck(overrideName, m2.skinFileDataIDs[i], forceOverrideName))
                                {
                                    NewFileManager.AddNewFile(m2.skinFileDataIDs[i], folder + "/" + currentModelName + "_lod" + (i - m2.nViews + 1).ToString().PadLeft(2, '0') + ".skin", overrideCheck(overrideName, m2.skinFileDataIDs[i], forceOverrideName), forceOverrideName);
                                    //Console.WriteLine("parent fdid for " + m2.skinFileDataIDs[i]  + " is " + fdid);
                                }
                            }
                            else
                            {
                                if (overrideCheck(overrideName, m2.skinFileDataIDs[i], forceOverrideName))
                                {
                                    NewFileManager.AddNewFile(m2.skinFileDataIDs[i], folder + "/" + currentModelName + i.ToString().PadLeft(2, '0') + ".skin", overrideCheck(overrideName, m2.skinFileDataIDs[i], forceOverrideName), forceOverrideName);
                                    // Console.WriteLine("parent fdid for " + m2.skinFileDataIDs[i] + " is " + fdid);
                                }
                            }
                        }

                        if (m2.animFileDataIDs != null)
                        {
                            for (var i = 0; i < m2.animFileDataIDs.Length; i++)
                            {
                                var anim = m2.animFileDataIDs[i];
                                if (anim.fileDataID == 0)
                                    continue;

                                if (overrideCheck(overrideName, anim.fileDataID, forceOverrideName))
                                    NewFileManager.AddNewFile(anim.fileDataID, folder + "/" + currentModelName + anim.animID.ToString().PadLeft(4, '0') + "-" + anim.subAnimID.ToString().PadLeft(2, '0') + ".anim", overrideCheck(overrideName, anim.fileDataID, forceOverrideName), forceOverrideName);
                            }
                        }

                        if (m2.textureFileDataIDs != null)
                        {
                            for (var i = 0; i < m2.textureFileDataIDs.Length; i++)
                            {
                                if (m2.textureFileDataIDs[i] == 0)
                                    continue;

                                if (overrideCheck(overrideName, m2.textureFileDataIDs[i], forceOverrideName))
                                    NewFileManager.AddNewFile(m2.textureFileDataIDs[i], folder + "/" + currentModelName + "_" + m2.textureFileDataIDs[i] + ".blp", overrideCheck(overrideName, m2.textureFileDataIDs[i], forceOverrideName), forceOverrideName);
                            }
                        }

                        if (m2.boneFileDataIDs != null)
                        {
                            for (var i = 0; i < m2.boneFileDataIDs.Length; i++)
                            {
                                if (m2.boneFileDataIDs[i] == 0)
                                    continue;

                                if (overrideCheck(overrideName, m2.boneFileDataIDs[i], forceOverrideName))
                                    NewFileManager.AddNewFile(m2.boneFileDataIDs[i], folder + "/" + currentModelName + "_" + m2.boneFileDataIDs[i] + ".bone", overrideCheck(overrideName, m2.boneFileDataIDs[i], forceOverrideName), forceOverrideName);
                            }
                        }

                        // TODO: Particles?


                        for (var i = 0; i < m2.events.Length; i++)
                        {
                            var ev = m2.events[i];
                            var soundType = "";
                            switch (ev.identifier)
                            {
                                case "$CSD":
                                    soundType = "csd";
                                    break;
                                case "$DSL":
                                    soundType = "loop";
                                    break;
                                case "$DSO":
                                    soundType = "oneshot";
                                    break;
                                case "$SND":
                                    soundType = "custom";
                                    break;
                            }

                            if (soundType != "" && soundKitFDIDMap.ContainsKey(ev.data))
                            {
                                foreach (var soundKitFDID in soundKitFDIDMap[ev.data])
                                {
                                    if (overrideCheck(overrideName, soundKitFDID, forceOverrideName))
                                    {
                                        NewFileManager.AddNewFile(soundKitFDID, "sound/doodad/go_" + currentModelName + "_" + soundType + "_" + soundKitFDID + ".ogg");
                                    }
                                }
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error reading M2:" + e.Message + "\n" + e.StackTrace);
                        continue;
                    }
                }
            }

            if (unkPrefixes.Count > 0)
            {
                var orderedUnkPrefixes = unkPrefixes.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                Console.WriteLine("Most unnamed prefixes (with more than 1 occurence):");
                foreach (var unkPrefix in orderedUnkPrefixes)
                {
                    if (unkPrefix.Value == 1)
                        continue;

                    Console.WriteLine(unkPrefix.Key + ": " + unkPrefix.Value);
                }
            }
        }

        private static bool overrideCheck(bool overrideName, uint fdid, bool forceOverride)
        {
            return fdid != 0 && (forceOverride || overrideName || !Namer.IDToNameLookup.ContainsKey((int)fdid) || Namer.placeholderNames.Contains((int)fdid));
        }

        public struct M2Model
        {
            public uint version;

            public uint physFileID;
            public uint skelFileID;

            public uint[] boneFileDataIDs;
            public uint[] skinFileDataIDs;
            public uint[] lod_skinFileDataIDs;
            public AFID[] animFileDataIDs;
            public uint[] textureFileDataIDs;
            public uint[] recursiveParticleModelFileIDs;
            public uint[] geometryParticleModelFileIDs;

            public string name;
            public uint nViews;
            public Event[] events;

        }
        public struct AFID
        {
            public short animID;
            public short subAnimID;
            public uint fileDataID;
        }

        public struct Event
        {
            public string identifier;
            public uint data;
            public uint bone;
            public float position_x;
            public float position_y;
            public float position_z;
            public ushort interpolationType;
            public ushort GlobalSequence;
            public uint nTimestampEntries;
            public uint ofsTimestampList;
        }

        private static M2Model ParseM2(MemoryStream ms)
        {
            var model = new M2Model();

            using (var bin = new BinaryReader(ms))
            {
                while (bin.BaseStream.Position < bin.BaseStream.Length)
                {
                    var chunkName = bin.ReadUInt32();
                    var chunkSize = bin.ReadUInt32();

                    if (chunkName == 0)
                        throw new Exception("M2 is encrypted");

                    var prevPos = bin.BaseStream.Position;
                    switch (chunkName)
                    {
                        case 'M' << 0 | 'D' << 8 | '2' << 16 | '1' << 24:
                            using (Stream m2stream = new MemoryStream(bin.ReadBytes((int)chunkSize)))
                            {
                                model = ParseYeOldeM2Struct(m2stream);
                            }

                            bin.BaseStream.Position = prevPos += chunkSize;

                            break;
                        case 'A' << 0 | 'F' << 8 | 'I' << 16 | 'D' << 24: // Animation file IDs
                            var afids = new AFID[chunkSize / 8];
                            for (var a = 0; a < chunkSize / 8; a++)
                            {
                                afids[a].animID = bin.ReadInt16();
                                afids[a].subAnimID = bin.ReadInt16();
                                afids[a].fileDataID = bin.ReadUInt32();
                            }
                            model.animFileDataIDs = afids;
                            break;
                        case 'B' << 0 | 'F' << 8 | 'I' << 16 | 'D' << 24: // Bone file IDs
                            var bfids = new uint[chunkSize / 4];
                            for (var b = 0; b < chunkSize / 4; b++)
                            {
                                bfids[b] = bin.ReadUInt32();
                            }
                            model.boneFileDataIDs = bfids;
                            break;
                        case 'S' << 0 | 'F' << 8 | 'I' << 16 | 'D' << 24: // Skin file IDs
                            var nEntries = chunkSize / 4;
                            var sfids = new uint[nEntries];
                            for (var s = 0; s < nEntries; s++)
                            {
                                sfids[s] = bin.ReadUInt32();
                            }
                            model.skinFileDataIDs = sfids;
                            break;
                        case 'P' << 0 | 'F' << 8 | 'I' << 16 | 'D' << 24: // Phys file ID
                            model.physFileID = bin.ReadUInt32();
                            break;
                        case 'S' << 0 | 'K' << 8 | 'I' << 16 | 'D' << 24: // Skel file ID
                            model.skelFileID = bin.ReadUInt32();
                            break;
                        case 'T' << 0 | 'X' << 8 | 'I' << 16 | 'D' << 24: // Texture file IDs
                            var txids = new uint[chunkSize / 4];
                            for (var t = 0; t < chunkSize / 4; t++)
                            {
                                txids[t] = bin.ReadUInt32();
                            }
                            model.textureFileDataIDs = txids;
                            break;
                        case 'R' << 0 | 'P' << 8 | 'I' << 16 | 'D' << 24: // Recursive particle file IDs
                            var rpids = new uint[chunkSize / 4];
                            for (var t = 0; t < chunkSize / 4; t++)
                            {
                                rpids[t] = bin.ReadUInt32();
                            }
                            model.recursiveParticleModelFileIDs = rpids;
                            break;
                        case 'G' << 0 | 'P' << 8 | 'I' << 16 | 'D' << 2: // Geometry particle file IDs
                            var gpids = new uint[chunkSize / 4];
                            for (var t = 0; t < chunkSize / 4; t++)
                            {
                                gpids[t] = bin.ReadUInt32();
                            }
                            model.geometryParticleModelFileIDs = gpids;
                            break;
                        default:
                            bin.BaseStream.Position += chunkSize;
                            break;
                    }
                }
            }

            return model;
        }

        private static M2Model ParseYeOldeM2Struct(Stream m2stream)
        {
            var model = new M2Model();

            var bin = new BinaryReader(m2stream);
            var header = bin.ReadUInt32();
            if (header != ('M' << 0 | 'D' << 8 | '2' << 16 | '0' << 24))
            {
                throw new Exception("Invalid M2 file!");
            }

            model.version = bin.ReadUInt32();
            var lenModelname = bin.ReadUInt32();
            var ofsModelname = bin.ReadUInt32();

            bin.ReadBytes(52);
            model.nViews = bin.ReadUInt32();
            bin.ReadBytes(184);
            var nEvents = bin.ReadUInt32();
            var ofsEvents = bin.ReadUInt32();

            if (lenModelname != 0)
            {
                bin.BaseStream.Position = ofsModelname;
                model.name = new string(bin.ReadChars((int)lenModelname));
                model.name = model.name.Remove(model.name.Length - 1); //remove last char, empty
            }

            model.events = ReadEvents(nEvents, ofsEvents, bin);

            return model;
        }

        private static Event[] ReadEvents(uint nEvents, uint ofsEvents, BinaryReader bin)
        {
            bin.BaseStream.Position = ofsEvents;
            var events = new Event[nEvents];
            for (var i = 0; i < nEvents; i++)
            {
                events[i] = new Event()
                {
                    identifier = System.Text.Encoding.ASCII.GetString(bin.ReadBytes(4)),
                    data = bin.ReadUInt32(),
                    bone = bin.ReadUInt32(),
                    position_x = bin.ReadSingle(),
                    position_y = bin.ReadSingle(),
                    position_z = bin.ReadSingle(),
                    interpolationType = bin.ReadUInt16(),
                    GlobalSequence = bin.ReadUInt16(),
                    nTimestampEntries = bin.ReadUInt32(),
                    ofsTimestampList = bin.ReadUInt32()
                };
            }
            return events;
        }
    }
}
