import { context, engine } from 'modaudio';
import { game } from 'atlyss';

let toggleCombatMusic = false;
let combatIntensity = 0;

export function pack_update() {
  const lastCombatState = toggleCombatMusic;

  combatIntensity = Math.max(0, combatIntensity - context.deltaTime);

  if (game.mainPlayer && game.mainPlayer._currentPlayerCondition == 3) // Dead
  {
    combatIntensity = 0;
  }
  else if (context.enemiesTargetingPlayer >= 2)
  {
    combatIntensity = Math.max(1.5 + context.enemiesTargetingPlayer - 2, combatIntensity);
  }
  else if (context.enemiesTargetingPlayer >= 1 && combatIntensity > 0)
  {
    combatIntensity = Math.max(1, combatIntensity);
  }
  
  toggleCombatMusic = toggleCombatMusic || combatIntensity > 0;

  if (lastCombatState != toggleCombatMusic) {
    console.log("Toggling combat music " + (toggleCombatMusic ? "on" : "off"));
  }

  engine.forceCombatMusic(toggleCombatMusic);
}