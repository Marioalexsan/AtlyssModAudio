namespace Marioalexsan.ModAudio;

public static class VanillaClipNames
{
    private static readonly string[] AllClipNames;
    private static readonly HashSet<string> ClipNamesHashed;

    static VanillaClipNames()
    {
        AllClipNames = typeof(VanillaClipNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(x => x.FieldType == typeof(string))
            .OrderBy(x => x.Name)
            .Select(x => (string)x.GetRawConstantValue())
            .ToArray();

        ClipNamesHashed = new HashSet<string>(AllClipNames);
    }

    internal static void GenerateReferenceFile(string location)
    {
        var lines = AllClipNames
            .Select(x =>
            {
                var meta = GetMetadata(x);
                return x + " | " + Enum.GetName(typeof(AudioGroup), meta.AudioGroup) + " | " + meta.Description;
            });

        var fileData = string.Join(Environment.NewLine,
            "This file serves as a reference for all of the available audio clip names within the game.",
            "To replace an audio clip, put custom audio under assets with the same name as the clip in here.",
            "The extension used (.mp3, .wav, .ogg) doesn't matter.",
            "",
            "Clip Name | Audio Group | Description",
            "",
            string.Join(Environment.NewLine, lines)
            );

        File.WriteAllText(location, fileData);
    }

    public static bool IsKnownClip(string clipName)
    {
        return ClipNamesHashed.Contains(clipName);
    }

    private struct Metadata(AudioGroup audioGroup, string description)
    {
        public AudioGroup AudioGroup = audioGroup;
        public string Description = description;
    }

    private enum AudioGroup
    {
        Unknown,
        Ambience,
        Game,
        GUI,
        Master,
        Music,
        Voice
    }

    private static Metadata GetMetadata(string audio)
    {
        return audio switch
        {
            _ => new(AudioGroup.Unknown, "<not documented yet>")
        };
    }

    public const int TotalClips = 482;
    public const string Absorb = "_absorb";
    public const string Acre_Bolt = "acre_bolt";
    public const string AmbBla = "_ambBla";
    public const string AmbCave01 = "_ambCave01";
    public const string AmbCave02 = "_ambCave02";
    public const string AmbCoast = "_ambCoast";
    public const string AmbCricket01 = "_ambCricket01";
    public const string AmbCuckoo = "_ambCuckoo";
    public const string AmbFire01 = "_ambFire01";
    public const string AmbFrst01 = "_ambFrst01";
    public const string AmbFrst02 = "_ambFrst02";
    public const string AmbFrst03 = "_ambFrst03";
    public const string AmbGlyph01 = "_ambGlyph01";
    public const string AmbGlyph02 = "_ambGlyph02";
    public const string AmbGlyph03 = "_ambGlyph03";
    public const string AmbGuest01 = "_ambGuest01";
    public const string AmbOutside01 = "_ambOutside01";
    public const string AmbOutside02 = "_ambOutside02";
    public const string AmbRiver01 = "_ambRiver01";
    public const string AmbUnderwater = "_ambUnderwater";
    public const string AmbWat_Cave = "_ambWat_cave";
    public const string AmbWater01 = "_ambWater01";
    public const string AmbWater02 = "_ambWater02";
    public const string AmbWaterfall01 = "_ambWaterfall01";
    public const string AmbWatfall = "_ambWatfall";
    public const string AmbWind01 = "_ambWind01";
    public const string AmbWind02 = "_ambWind02";
    public const string AmbWind02_0 = "_ambWind02_0";
    public const string AmbWind03 = "_ambWind03";
    public const string AmbWind04 = "_ambWind04";
    public const string AmbWind05 = "_ambWind05";
    public const string AmbWind06 = "_ambWind06";
    public const string AmbWind07 = "_ambWind07";
    public const string AmbWindChime01 = "_ambWindChime01";
    public const string Amb_Jungle00 = "_amb_jungle00";
    public const string Amb_Jungle01 = "_amb_jungle01";
    public const string Amb_Jungle02 = "_amb_jungle02";
    public const string Amb_Rain01 = "_amb_rain01";
    public const string Angela_Aura = "angela_aura";
    public const string AtlyssSet_Catacomb_Action5 = "_atlyssSet_catacomb_action5";
    public const string AtlyssSet_Catacomb_Amb3 = "_atlyssSet_catacomb_amb3";
    public const string AtlyssSet_Track01 = "_atlyssSet_track01";
    public const string AttackClick = "attackClick";
    public const string AttackMiss = "_attackMiss";
    public const string AttributeGlyph = "_attributeGlyph";
    public const string BeamStatue_BeamLoop = "_beamStatue_beamLoop";
    public const string BeamStatue_ProneLoop = "_beamStatue_proneLoop";
    public const string BellSwing01 = "_bellSwing01";
    public const string BellSwing02 = "_bellSwing02";
    public const string BellSwing03 = "_bellSwing03";
    public const string BellSwing04 = "_bellSwing04";
    public const string BladeTrap_Init = "_bladeTrap_init";
    public const string BladeTrap_SpinLoop = "_bladeTrap_spinLoop";
    public const string BladeTrap_SpinLoop2 = "_bladeTrap_spinLoop2";
    public const string BlockBreak = "_blockBreak";
    public const string BlockHit = "_blockHit";
    public const string BobbyVox01 = "bobbyVox01";
    public const string BobbyVox02 = "bobbyVox02";
    public const string BobbyVox03 = "bobbyVox03";
    public const string BobbyVox04 = "bobbyVox04";
    public const string BobbyVox05 = "bobbyVox05";
    public const string BobbyVox06 = "bobbyVox06";
    public const string BobbyVox07 = "bobbyVox07";
    public const string BombBiggenEX = "bombBiggenEX";
    public const string BombHitChar = "bombHitChar";
    public const string BombKick = "bombKick";
    public const string Book_Handled_1 = "book handled 1";
    public const string Bow_Fire01 = "bow_fire01";
    public const string Bow_TakeOut = "bow_takeOut";
    public const string Break01 = "_break01";
    public const string BreakBox01 = "_breakBox01";
    public const string Buff_Resolute = "buff_resolute";
    public const string BuyItem = "_buyItem";
    public const string ByrdleTalk01 = "_byrdleTalk01";
    public const string ByrdleTalk02 = "_byrdleTalk02";
    public const string ByrdleTalk03 = "_byrdleTalk03";
    public const string ByrdleTalk04 = "_byrdleTalk04";
    public const string Cannonbug_BeginMove = "cannonbug_beginMove";
    public const string Cannonbug_CannonShot = "cannonbug_cannonShot";
    public const string Cannonbug_ChargeShot = "cannonbug_chargeShot";
    public const string Cannonbug_FrontalAttack = "cannonbug_frontalAttack";
    public const string Cannonbug_Spin = "cannonbug_spin";
    public const string Carbuncle_Death = "_carbuncle_death";
    public const string Carbuncle_Hurt00 = "_carbuncle_hurt00";
    public const string CastChannelShadow = "castChannelShadow";
    public const string CastChannelShadow2 = "castChannelShadow2";
    public const string CastEarth = "_castEarth";
    public const string CastLoop_00 = "_castLoop_00";
    public const string CastLoop_01 = "_castLoop_01";
    public const string CastMysticSpell = "_castMysticSpell";
    public const string CastWater = "_castWater";
    public const string Cast_00 = "_cast_00";
    public const string Cast_01 = "_cast_01";
    public const string Cast_02 = "_cast_02";
    public const string Cast_03 = "_cast_03";
    public const string Cast_04 = "_cast_04";
    public const string Cast_05 = "_cast_05";
    public const string Cast_06 = "_cast_06";
    public const string Cast_07 = "_cast_07";
    public const string Cast_FlameBurst = "cast_flameBurst";
    public const string Cast_Holy = "_cast_holy";
    public const string Cast_Octane = "cast_octane";
    public const string ChangTalk01 = "_changTalk01";
    public const string ChangTalk02 = "_changTalk02";
    public const string ChangTalk03 = "_changTalk03";
    public const string ChangTalk04 = "_changTalk04";
    public const string ChannelEarthSpell = "_channelEarthSpell";
    public const string ChannelMysticSpell = "_channelMysticSpell";
    public const string ChestOpen = "chestOpen";
    public const string Chime02 = "_chime02";
    public const string Chime03 = "_chime03";
    public const string Chime04 = "_chime04";
    public const string Clap01 = "_clap01";
    public const string ClearSkill = "_clearSkill";
    public const string CloseShop = "_closeShop";
    public const string Coin01 = "_coin01";
    public const string CoinSell = "_coinSell";
    public const string Collide01 = "_collide01";
    public const string ConsumableHeal = "consumableHeal";
    public const string Cop_Turretcharge = "cop_turretcharge";
    public const string CreepAggroNotif = "_creepAggroNotif";
    public const string CreepSfx_SlimeAtk1 = "_creepSfx_slimeAtk1";
    public const string CreepSfx_SlimeAtk2 = "_creepSfx_slimeAtk2";
    public const string CreepSfx_SlimeAtk3 = "_creepSfx_slimeAtk3";
    public const string CreepSfx_SlimeDeath = "_creepSfx_slimeDeath";
    public const string CreepSfx_SlimeHit = "_creepSfx_slimeHit";
    public const string CreepSfx_SlimeHurt = "_creepSfx_slimeHurt";
    public const string CreepSpawn = "_creepSpawn";
    public const string Crit = "_crit";
    public const string CrossExplodeSfx01 = "_crossExplodeSfx01";
    public const string CrossLoopSfx01 = "_crossLoopSfx01";
    public const string CurrencyTally = "_currencyTally";
    public const string Debuff_Bleed = "debuff_bleed";
    public const string Debuff_Freeze = "debuff_freeze";
    public const string Debuff_Freeze_Old1 = "debuff_freeze-old1";
    public const string Debuff_Poison = "debuff_poison";
    public const string DungeonFloorSwitch_Pressed = "_dungeonFloorSwitch_pressed";
    public const string DungeonFloorSwitch_Unpressed = "_dungeonFloorSwitch_unpressed";
    public const string DungeonLock_Open = "_dungeonLock_open";
    public const string Effect_Demon01 = "_effect_demon01";
    public const string Electric01 = "_electric01";
    public const string EnemyPreAttack = "enemyPreAttack";
    public const string Enemyalert = "enemyalert";
    public const string Enok_Vox01 = "_enok_vox01";
    public const string Enok_Vox02 = "_enok_vox02";
    public const string Enok_Vox03 = "_enok_vox03";
    public const string Enok_Vox04 = "_enok_vox04";
    public const string Enok_Vox05 = "_enok_vox05";
    public const string Enok_Vox06 = "_enok_vox06";
    public const string Enok_Vox07 = "_enok_vox07";
    public const string Enok_Vox08 = "_enok_vox08";
    public const string Equip01 = "_equip01";
    public const string Equip02 = "_equip02";
    public const string ExpPickup_F = "expPickup_f";
    public const string Expression_Surprise = "_expression_surprise";
    public const string Fanfale3 = "fanfale3";
    public const string Fanfale4 = "fanfale4";
    public const string Fistcuffs_Swing01 = "_fistcuffs_swing01";
    public const string Fistcuffs_Swing02 = "_fistcuffs_swing02";
    public const string Fistcuffs_Swing03 = "_fistcuffs_swing03";
    public const string Fistcuffs_Swing04 = "_fistcuffs_swing04";
    public const string Fistcuffs_Swing05 = "_fistcuffs_swing05";
    public const string FloatSkull_Attack01 = "_floatSkull_attack01";
    public const string FloatSkull_Attack02 = "_floatSkull_attack02";
    public const string FloatSkull_Death = "_floatSkull_death";
    public const string FloatSkull_Hurt = "_floatSkull_hurt";
    public const string FluxSpear01 = "_fluxSpear01";
    public const string Focus = "focus";
    public const string Focusoff = "focusoff";
    public const string FootStep_Basic = "footStep_basic";
    public const string FootStep_Grass = "footStep_grass";
    public const string FootStep_Grass02 = "footStep_grass02";
    public const string FootStep_Stone = "footStep_stone";
    public const string FootStep_Water = "footStep_water";
    public const string FootStep_Wood = "footStep_wood";
    public const string FootstepGrass02 = "_footstepGrass02";
    public const string GeistLaugh = "_geistLaugh";
    public const string GeistLaugh02 = "_geistLaugh02";
    public const string Geist_Attack01 = "_geist_attack01";
    public const string Geist_Attack02 = "_geist_attack02";
    public const string Geist_Death = "_geist_death";
    public const string Geist_Hurt = "_geist_hurt";
    public const string GetClassSfx = "_getClassSfx";
    public const string Gib = "_gib";
    public const string GlassBreak = "glassBreak";
    public const string GlassBreak2 = "glassBreak2";
    public const string Golem_Death01 = "_golem_death01";
    public const string Golem_Hurt01 = "_golem_hurt01";
    public const string Golem_Hurt02 = "_golem_hurt02";
    public const string Guard_Block = "guard_block";
    public const string Hampter_Defend = "hampter_defend";
    public const string Hampter_Login = "hampter_login";
    public const string HealConsumable = "_healConsumable";
    public const string HeavyInit01 = "_heavyInit01";
    public const string HeavyInit02 = "_heavyInit02";
    public const string HeavyInit03 = "_heavyInit03";
    public const string HeavySheath = "_heavySheath";
    public const string Homerun_Hit = "_homerun_hit";
    public const string ImbueSkillSfx = "_imbueSkillSfx";
    public const string ImpTalk01 = "_impTalk01";
    public const string ImpTalk02 = "_impTalk02";
    public const string ImpTalk03 = "_impTalk03";
    public const string ImpTalk04 = "_impTalk04";
    public const string ImpTalk05 = "_impTalk05";
    public const string ImpTalk06 = "_impTalk06";
    public const string IntroNoise_00 = "_introNoise_00";
    public const string IntroNoise_01 = "_introNoise_01";
    public const string IntroNoise_02 = "_introNoise_02";
    public const string ItemDrop_Rare = "_itemDrop_rare";
    public const string ItemEntryPlace01 = "_itemEntryPlace01";
    public const string ItemEntryPlace02 = "_itemEntryPlace02";
    public const string ItemObject_Pickup = "itemObject_pickup";
    public const string ItemObject_Spawn = "itemObject_spawn";
    public const string KinggolemDeath_00 = "_kinggolemDeath_00";
    public const string KuboldTalk01 = "_kuboldTalk01";
    public const string KuboldTalk02 = "_kuboldTalk02";
    public const string KuboldTalk03 = "_kuboldTalk03";
    public const string KuboldTalk04 = "_kuboldTalk04";
    public const string KuboldTalk05 = "_kuboldTalk05";
    public const string Land_Grass01 = "land_grass01";
    public const string Land_Stone01 = "land_stone01";
    public const string Land_Water = "land_water";
    public const string Land_Wood = "land_wood";
    public const string LargeFootstep01 = "_largeFootstep01";
    public const string LedgeGrab = "_ledgeGrab";
    public const string Levelup = "levelup";
    public const string LeverSfx01 = "_leverSfx01";
    public const string LexiconBell = "_lexiconBell";
    public const string LexiconClose = "_lexiconClose";
    public const string LexiconOpen = "_lexiconOpen";
    public const string LightSheath = "_lightSheath";
    public const string Lockon = "lockon";
    public const string Lockout = "lockout";
    public const string LowHealthPulse01 = "_lowHealthPulse01";
    public const string LowHealthWarning = "_lowHealthWarning";
    public const string LungePower = "_lungePower";
    public const string MediumSheath = "_mediumSheath";
    public const string MeeleSkillCharge = "_meeleSkillCharge";
    public const string Mekboar_ChargeAtk01 = "_mekboar_chargeAtk01";
    public const string Mekboar_ChargeAtk02 = "_mekboar_chargeAtk02";
    public const string Mekboar_ChargeAtk03 = "_mekboar_chargeAtk03";
    public const string Mekboar_Death = "_mekboar_death";
    public const string Mekboar_Hurt = "_mekboar_hurt";
    public const string MeleeSkillLoop = "_meleeSkillLoop";
    public const string Melee_Swipe = "melee_swipe";
    public const string MouthEnemy_Death01 = "mouthEnemy_death01";
    public const string MouthEnemy_Hurt01 = "mouthEnemy_hurt01";
    public const string MouthEnemy_Hurt02 = "mouthEnemy_hurt02";
    public const string MouthEnemy_Vomit01 = "mouthEnemy_vomit01";
    public const string MouthEnemy_Vomit02 = "mouthEnemy_vomit02";
    public const string Mover_MetalGateClose = "_mover_metalGateClose";
    public const string Mover_MetalGateLoop = "_mover_metalGateLoop";
    public const string Mover_MetalGateOpen = "_mover_metalGateOpen";
    public const string Mu_AmbCrispr = "_mu_ambCrispr";
    public const string Mu_Botany = "_mu_botany";
    public const string Mu_Cahoots = "_mu_cahoots";
    public const string Mu_Calp = "_mu_calp";
    public const string Mu_Cane = "_mu_cane";
    public const string Mu_Discover01 = "_mu_discover01";
    public const string Mu_Discover02 = "_mu_discover02";
    public const string Mu_Discover03 = "_mu_discover03";
    public const string Mu_Discover04 = "_mu_discover04";
    public const string Mu_Discover05 = "_mu_discover05";
    public const string Mu_Ecka = "_mu_ecka";
    public const string Mu_Flyby = "_mu_flyby";
    public const string Mu_Haven = "_mu_haven";
    public const string Mu_Hell01 = "_mu_hell01";
    public const string Mu_Hell02 = "_mu_hell02";
    public const string Mu_Laid = "_mu_laid";
    public const string Mu_Lethargy = "_mu_lethargy";
    public const string Mu_NouCove = "mu_nouCove";
    public const string Mu_Photo = "_mu_photo";
    public const string Mu_Sailex = "_mu_sailex";
    public const string Mu_Select1 = "mu_select1";
    public const string Mu_Selee = "_mu_selee";
    public const string Mu_SnatchNight = "_mu_snatchNight";
    public const string Mu_Snatchsprings = "mu_snatchsprings";
    public const string Mu_Tex01 = "_mu_tex01";
    public const string Mu_Wasp = "_mu_wasp";
    public const string Mu_Wonton = "_mu_wonton";
    public const string Mu_Wonton5 = "_mu_wonton5";
    public const string Music_Whisper = "music_whisper";
    public const string NovaSkill_Init01 = "_novaSkill_init01";
    public const string NovaSkill_Init02 = "_novaSkill_init02";
    public const string OpenShop = "_openShop";
    public const string Option = "option";
    public const string PartyInviteSfx = "_partyInviteSfx";
    public const string Party_Init01 = "_party_init01";
    public const string Party_Join = "_party_join";
    public const string Pickup01 = "_pickup01";
    public const string Pickup02 = "_pickup02";
    public const string PlayerPort = "_playerPort";
    public const string Player_Dash = "player_dash";
    public const string Player_InitDeath = "_player_initDeath";
    public const string Player_InitDeath2 = "_player_initDeath2";
    public const string Player_Jiggle2 = "player_jiggle2";
    public const string Player_Jump = "player_jump";
    public const string PolearmSwing01 = "_polearmSwing01";
    public const string PolearmSwing02 = "_polearmSwing02";
    public const string PolearmSwing03 = "_polearmSwing03";
    public const string PolearmTakeOut01 = "_polearmTakeOut01";
    public const string PoonBounce = "_poonBounce";
    public const string PoonBounce02 = "_poonBounce02";
    public const string PoonTalk01 = "_poonTalk01";
    public const string PoonTalk02 = "_poonTalk02";
    public const string PoonTalk03 = "_poonTalk03";
    public const string PoonTalk04 = "_poonTalk04";
    public const string PoonTalk05 = "_poonTalk05";
    public const string PoonTalk06 = "_poonTalk06";
    public const string Port = "port";
    public const string PortalGlyph = "_portalGlyph";
    public const string PortalInteract = "_portalInteract";
    public const string PrismSkill_Init01 = "_prismSkill_init01";
    public const string PrismSkill_Loop01 = "_prismSkill_loop01";
    public const string Pushblock_Loop = "_pushblock_loop";
    public const string Pushblock_Stop = "_pushblock_stop";
    public const string PvpFlagInit = "_pvpFlagInit";
    public const string QuestAbandon = "_questAbandon";
    public const string QuestAccept = "_questAccept";
    public const string QuestProgressTick = "_questProgressTick";
    public const string QuestTurnIn = "_questTurnIn";
    public const string RageSfx01 = "_rageSfx01";
    public const string Railgun_Lv3 = "railgun_lv3";
    public const string ResShrineLoop = "_resShrineLoop";
    public const string Rock_Impact = "rock_impact";
    public const string RoosterDaySfx01 = "_roosterDaySfx01";
    public const string RopeClimb = "_ropeClimb";
    public const string SallyVox01 = "_sallyVox01";
    public const string SallyVox02 = "_sallyVox02";
    public const string SallyVox03 = "_sallyVox03";
    public const string SallyVox04 = "_sallyVox04";
    public const string SallyVox05 = "_sallyVox05";
    public const string SallyVox06 = "_sallyVox06";
    public const string SallyVox07 = "_sallyVox07";
    public const string Sally_Sweep = "_sally_sweep";
    public const string Satch_Explodelv2 = "satch_explodelv2";
    public const string ScepterCharge = "_scepterCharge";
    public const string ScepterChargeWeak = "_scepterChargeWeak";
    public const string ScepterProjectileBurst = "scepterProjectileBurst";
    public const string SeedPickup = "seedPickup";
    public const string Seekr_Equip = "seekr_equip";
    public const string SkillSet = "_skillSet";
    public const string SkritVox_00 = "_skritVox_00";
    public const string SkritVox_01 = "_skritVox_01";
    public const string SkritVox_02 = "_skritVox_02";
    public const string SkritVox_03 = "_skritVox_03";
    public const string Slap = "_slap";
    public const string SlimeDiva_AttackAoeSwing = "_slimeDiva_attackAoeSwing";
    public const string SlimeDiva_ChargeTitGun = "_slimeDiva_chargeTitGun";
    public const string SlimeDiva_Death = "_slimeDiva_death";
    public const string SlimeDiva_FloatMoveLoop = "_slimeDiva_floatMoveLoop";
    public const string SlimeDiva_Hurt01 = "_slimeDiva_hurt01";
    public const string SlimeDiva_Laugh01 = "_slimeDiva_laugh01";
    public const string SlimeDiva_Laugh02 = "_slimeDiva_laugh02";
    public const string SlimeDiva_Moan01 = "_slimeDiva_moan01";
    public const string SlimeDiva_SlimeOrb_Bounce = "_slimeDiva_slimeOrb_bounce";
    public const string SlimeDiva_SlimeOrb_Explode = "_slimeDiva_slimeOrb_explode";
    public const string SlimeDiva_TitBullet = "_slimeDiva_titBullet";
    public const string SmallClap = "_smallClap";
    public const string Snd_GrubdogBite = "snd_grubdogBite";
    public const string Snd_GrubdogConsume = "snd_grubdogConsume";
    public const string Snd_SummonAngel = "snd_summonAngel";
    public const string Spawn03 = "_spawn03";
    public const string SpellCancel = "spellCancel";
    public const string SpellChannel_Fire = "spellChannel_fire";
    public const string SpellChannel_Vector1 = "spellChannel_vector1";
    public const string SpellChannel_Vector2 = "spellChannel_vector2";
    public const string SpellChannel_Water = "_spellChannel_water";
    public const string SpellLearnChannel = "spellLearnChannel";
    public const string SpellWater_Droplet = "_spellWater_droplet";
    public const string Spellsfx00 = "_spellsfx00";
    public const string SpikePanel_Close = "_spikePanel_close";
    public const string SpikePanel_Init = "_spikePanel_init";
    public const string SpinScepter = "_spinScepter";
    public const string SpinWeapon01 = "_spinWeapon01";
    public const string Stump_BallProj = "_stump_ballProj";
    public const string Stump_Death = "_stump_death";
    public const string Stump_Death02 = "_stump_death02";
    public const string Stump_Hurt = "_stump_hurt";
    public const string Stump_Hurt02 = "_stump_hurt02";
    public const string Stump_ProjPuke = "_stump_projPuke";
    public const string SummonCollide = "summonCollide";
    public const string SummonSelect = "summonSelect";
    public const string SwimUp = "_swimUp";
    public const string Swing03 = "_swing03";
    public const string Swing04 = "_swing04";
    public const string SwingHeavy = "_swingHeavy";
    public const string SwingLight = "_swingLight";
    public const string SwingMedium = "_swingMedium";
    public const string TakeOutBell = "_takeOutBell";
    public const string TakeOutBell2 = "_takeOutBell2";
    public const string TakeOutBell3 = "_takeOutBell3";
    public const string Target_Blip = "target_blip";
    public const string Thud01 = "_thud01";
    public const string TriggerMessageTone = "_triggerMessageTone";
    public const string Type = "type";
    public const string UiBookFlip01 = "_uiBookFlip01";
    public const string UiClick01 = "_uiClick01";
    public const string UiClick02 = "_uiClick02";
    public const string UiClick03 = "_uiClick03";
    public const string UiClose = "_uiClose";
    public const string UiGameTabMenu = "_uiGameTabMenu";
    public const string UiGameTabMenuClose = "_uiGameTabMenuClose";
    public const string UiHover = "_uiHover";
    public const string Ui_ActionBarApply = "ui_actionBarApply";
    public const string Ui_ActionBarCancel = "ui_actionBarCancel";
    public const string Ui_Button = "ui_button";
    public const string Ui_Click01 = "ui_click01";
    public const string Ui_ErrorPrompt = "ui_errorPrompt";
    public const string Ui_Getachievement = "ui_getachievement";
    public const string Ui_Input01 = "ui_input01";
    public const string Ui_Input02 = "ui_input02";
    public const string Ui_Select01 = "ui_select01";
    public const string Ui_WeaponQuickSwitch = "ui_weaponQuickSwitch";
    public const string UnlearnSfx = "_unlearnSfx";
    public const string Vc_Randy1 = "vc_randy1";
    public const string Vc_Randy2 = "vc_randy2";
    public const string Vc_Randy3 = "vc_randy3";
    public const string Vc_Randy4 = "vc_randy4";
    public const string Vc_Suki1 = "vc_suki1";
    public const string Vc_Suki2 = "vc_suki2";
    public const string Vc_Suki3 = "vc_suki3";
    public const string Vc_Suki4 = "vc_suki4";
    public const string Vc_Suki5 = "vc_suki5";
    public const string Vc_Suki6 = "vc_suki6";
    public const string Vc_Suki7 = "vc_suki7";
    public const string Vc_Typewriter = "vc_typewriter";
    public const string Vivian_Vox00 = "_vivian_vox00";
    public const string Vivian_Vox01 = "_vivian_vox01";
    public const string Vivian_Vox02 = "_vivian_vox02";
    public const string Vivian_Vox03 = "_vivian_vox03";
    public const string Vivian_Vox04 = "_vivian_vox04";
    public const string Vivian_Vox05 = "_vivian_vox05";
    public const string VolleyLoop = "_volleyLoop";
    public const string VolleyLoop2 = "_volleyLoop2";
    public const string VolleyLoop3 = "_volleyLoop3";
    public const string Vox_Angela01 = "vox_angela01";
    public const string Vox_Angela02 = "vox_angela02";
    public const string Vox_Angela03 = "vox_angela03";
    public const string Vox_Angela04 = "vox_angela04";
    public const string Vox_Angela05 = "vox_angela05";
    public const string Vox_Angela06 = "vox_angela06";
    public const string Warble = "warble";
    public const string Warp1 = "warp1";
    public const string WaterEnter = "_waterEnter";
    public const string WaterExit = "_waterExit";
    public const string WaterPillarInit = "_waterPillarInit";
    public const string WaterWalk = "_waterWalk";
    public const string WeaponCharge = "_weaponCharge";
    public const string WeaponHitWall = "_weaponHitWall";
    public const string WeaponHit_Air_Average = "weaponHit_Air(average)";
    public const string WeaponHit_Air_Heavy = "weaponHit_Air(heavy)";
    public const string WeaponHit_Air_Light = "weaponHit_Air(light)";
    public const string WeaponHit_Earth_Average = "weaponHit_Earth(average)";
    public const string WeaponHit_Earth_Heavy = "weaponHit_Earth(heavy)";
    public const string WeaponHit_Earth_Light = "weaponHit_Earth(light)";
    public const string WeaponHit_Fire_Average = "weaponHit_Fire(average)";
    public const string WeaponHit_Fire_Heavy = "weaponHit_Fire(heavy)";
    public const string WeaponHit_Fire_Light = "weaponHit_Fire(light)";
    public const string WeaponHit_Holy_Average = "weaponHit_Holy(average)";
    public const string WeaponHit_Holy_Heavy = "weaponHit_Holy(heavy)";
    public const string WeaponHit_Holy_Light = "weaponHit_Holy(light)";
    public const string WeaponHit_Normal_Average = "weaponHit_Normal(average)";
    public const string WeaponHit_Normal_Heavy = "weaponHit_Normal(heavy)";
    public const string WeaponHit_Normal_Light = "weaponHit_Normal(light)";
    public const string WeaponHit_Shadow_Average = "weaponHit_Shadow(average)";
    public const string WeaponHit_Shadow_Heavy = "weaponHit_Shadow(heavy)";
    public const string WeaponHit_Shadow_Light = "weaponHit_Shadow(light)";
    public const string WeaponHit_Water_Average = "weaponHit_Water(average)";
    public const string WeaponHit_Water_Heavy = "weaponHit_Water(heavy)";
    public const string WeaponHit_Water_Light = "weaponHit_Water(light)";
    public const string WeaponOverHeat = "weaponOverHeat";
    public const string WeaponPutAway01 = "_weaponPutAway01";
    public const string Weapon_AxePound = "weapon_axePound";
    public const string Weapon_Pickup = "weapon_pickup";
    public const string Weapon_ScepterSheath = "weapon_scepterSheath";
    public const string Weapon_StaffAoe_Autumn = "weapon_staffAoe(autumn)";
    public const string Weapon_StaffAttack_Autumn = "weapon_staffAttack(autumn)";
    public const string Weapon_WickCurseLv1 = "weapon_wickCurseLv1";
    public const string Wick_Equip = "wick_equip";
    public const string Wickhitlv1 = "wickhitlv1";
    public const string WolfHowl = "_wolfHowl";
}
