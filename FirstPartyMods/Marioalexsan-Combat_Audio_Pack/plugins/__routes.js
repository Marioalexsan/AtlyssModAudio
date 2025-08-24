import { context, engine } from 'modaudio';
import { game } from 'atlyss';

let toggleCombatMusic = false;
let combatTime = 0;

let combatType = "normal";
let inPvp = false;

export function pack_update() {
  const lastCombatState = toggleCombatMusic;
  const lastCombatType = combatType;
  const lastPvp = inPvp;

  const aggroedEnemies = context.aggroedEnemies;
  const mainPlayer = game.mainPlayer;

  toggleCombatMusic = false;
  combatType = "normal";
  combatTime = Math.max(0, combatTime - context.deltaTime);

  const timeSinceLastPvp = context.secondsSinceGameStart - context.mainPlayerLastPvpEventAt;

  if (timeSinceLastPvp <= 3) {
    inPvp = true;
  }

  if (mainPlayer && mainPlayer._currentPlayerCondition == 3) {
    // Remind yourself that overconfidence is a slow and insidious killer
    combatTime = 0;
    inPvp = false;
  }

  if (inPvp) {
    // Conditions to break PVP:
    // - You die (handled by death condtion)
    // - 20 seconds have passed since last PVP event
    // - 5 seconds have passed since last PVP event and the player involved in it is dead
    // - Players have different map instances (such as from teleporting)

    if (timeSinceLastPvp >= 20) {
      inPvp = false;
    }

    if (timeSinceLastPvp >= 5 && context.lastPlayerPvp && context.lastPlayerPvp._currentPlayerCondition == 3) {
      inPvp = false;
    }

    if (context.lastPlayerPvp && context.lastPlayerPvp._playerMapInstance._mapName != mainPlayer._playerMapInstance._mapName) {
      inPvp = false;
    }
  }
  
  if (aggroedEnemies.length >= 1) {
    if (combatTime > 0) {
      combatTime = Math.max(3, combatTime);
    }

    let threat = 0;

    for (const enemy of aggroedEnemies) {
      let creepThreat = 1;

      const levelDiff = enemy._creepLevel - mainPlayer._pStats._currentLevel;

      if (levelDiff >= 0) {
        creepThreat += 1.0;
      }

      if (levelDiff >= 5) {
        creepThreat += 1.0;
      }

      if (enemy._scriptCreep._isElite) {
        creepThreat += 1.0;
      }

      threat += creepThreat;

      if (enemy._scriptCreep._playMapInstanceActionMusic) {
        combatType = "miniboss";
      }
    }

    if (threat >= 3) {
      combatTime = Math.max(5, combatTime);
    }
  }
  
  if (inPvp || combatTime > 0) {
    toggleCombatMusic = true;
  }

  if (lastCombatState != toggleCombatMusic) {
    console.log("Toggling combat music " + (toggleCombatMusic ? "on" : "off"));
  }

  if (lastCombatType != combatType) {
    console.log("Toggling combat type to " + combatType);
  }

  if (lastPvp != inPvp) {
    console.log("Toggling PVP mode to " + (inPvp ? "on" : "off"));
  }

  engine.forceCombatMusic(toggleCombatMusic);
}

export function target_group_effold_terrace(route) {
  if (combatType === "normal") {
    route.targetGroup = "regular_combat";
  }
  else {
    route.targetGroup = "slime_diva_combat";
  }
}

export function target_group_tuul_valley(route) {
  if (game.inGameUI._reigonTitle === "Tuul Enclave") {
    route.targetGroup = "enclave";
  }
  else {
    route.targetGroup = "valley";
  }
}

export function target_group_sanctumarena(route) {
  if (game.mainPlayer._playerMapInstance._mapName === "Sanctum Arena") {
    route.targetGroup = "arena";
  }
  else {
    route.targetGroup = "nonarena";
  }
}