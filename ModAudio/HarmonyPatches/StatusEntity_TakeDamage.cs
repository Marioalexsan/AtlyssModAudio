using HarmonyLib;
using Marioalexsan.ModAudio.Scripting;
using Marioalexsan.ModAudio.Scripting.Data;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch(typeof(StatusEntity), nameof(StatusEntity.Take_Damage))]
static class StatusEntity_TakeDamage
{
    static void Postfix(StatusEntity __instance, in DamageStruct _dmgStruct)
    {
        bool mainPlayerTookPvpDamage =
            __instance._isPlayer != null
            && _dmgStruct._statusEntity != null
            && _dmgStruct._statusEntity._isPlayer != null
            && __instance._isPlayer == Player._mainPlayer
            && _dmgStruct._colliderType != CombatColliderType.SUPPORT
            && __instance._isPlayer._currentPlayerCondition == PlayerCondition.ACTIVE
            && !__instance._isPlayer._bufferingStatus;

        bool mainPlayerDealtPvpDamage =
            __instance._isPlayer != null
            && _dmgStruct._statusEntity != null
            && _dmgStruct._statusEntity._isPlayer != null
            && __instance._isPlayer != Player._mainPlayer
            && _dmgStruct._colliderType != CombatColliderType.SUPPORT
            && __instance._isPlayer._currentPlayerCondition == PlayerCondition.ACTIVE
            && !__instance._isPlayer._bufferingStatus
            && _dmgStruct._statusEntity?._isPlayer == Player._mainPlayer;

        if (mainPlayerDealtPvpDamage || mainPlayerTookPvpDamage)
        {
            ContextData.MainPlayerLastPvpEventAt = Time.realtimeSinceStartupAsDouble;

            if (mainPlayerTookPvpDamage)
            {
                var player = _dmgStruct._statusEntity?._isPlayer;
                ContextData.LastPlayerPvp = player;
            }

            else if (mainPlayerDealtPvpDamage)
            {
                var player = __instance._isPlayer;
                ContextData.LastPlayerPvp = player;
            }
        }
    }
}
