if(phase === Phase.WaitForFiringExchangeStarted)
{
    if(hasFireExchanged())
    {
        msgBoxDelay(`
A ship starts to fire!

Select a ship from the firing group (typically, Japanese ships fire first in this scenario). Red lines will appear, showing the ship firing at its target with its primary, secondary, tertiary, or RF batteries.

Open the ship log editor and go to the Battery tab. There you can find information about the firing process; the most interesting entries are the ammunition and Processing Seconds.

Batteries carry different types of ammunition, and the AI will use the optimal ammo when firing.

Processing Seconds increase as time advances until the threshold determined by the Rate of Fire is reached. At that point, a shot is resolved and Processing Seconds are reset to zero. No flying shells are modeled (although launched torpedoes are modeled).

Different mounts have different firing arcs defined in their ship class correspondence record. Generally, a ship's broadside firepower is stronger than its forward or aft firepower. However, presenting the broadside angle also makes the ship easier to hit.

Turn off "Current Only" (which clears logs as time advances, so only the "current" log is shown) in the global log panel (bottom-left corner).

Advance time until a hit is scored.
`, 0.3);

        phase = Phase.WaitForAHitScored;
    }
}
else if(phase === Phase.WaitForAHitScored)
{
    if(isHitScored())
    {
        msgBox(`
A hit is scored!

A log entry will appear in the global log panel. You can also check the log at the individual ship level by opening the ship log editor for the damaged ship and clicking the "Detail" button in the Basic tab.

The linear part of this tutorial is now complete. Notifications for concept like Damage Effect, Sunk, and Victory will be provided when they occur for the first time. Feel free to control the two groups and continue combat until only one remains on the battlefield.
`)
        phase = Phase.End;
    }
}

if(!damageEffectPrompted && hasAnyDamageEffect())
{
    damageEffectPrompted = true;

    msgBox(`
A Damage Effect (Sub State) is applied to a ship!

You may determine affected ships by global log, or open the Ship Log Editor and switch to the Damage Effect tab to check each ship using the keyboard's up/down keys.

Each hit inflicts a "homogeneous" amount of damage point loss, while more "heterogeneity" and location-specific damage—such as magazine explosions, flooding, rudder disablement, FCS misalignment, and so on—is handled by damage effects.

Some damage effects may be permanent or temporary, and they are displayed in the Damage Effect tab. Certain effects, especially shipboard fires, is damage controllable. They may tend to be worsen if no Damage Control points are allocated to them. The AI will use its Damage Control points to contain Damage Effects according to default priorities.
`);

}

if(!sunkPrompted && hasAnySunk())
{
    sunkPrompted = true;

    msgBox(`
A ship has been sunk!

As you may have noticed, in the First Sino-Japanese War, or Seekrieg 5, sinking is not guaranteed when the damage point percentage reaches 100%. A ship may sink before reaching 100%, or it might not sink even after exceeding 1000%. The damage point primarily establishes a probability distribution—ships tend to become combat-ineffective at 100%, but total destruction isn't certain.

Mechanically, damage points can trigger critical "General" Damage Effects when certain damage point tier (percentages thresholds) are crossed. A ship will sink if too many tiers are exceeded within a short period. However, it is possible—with a non-negligible probability—to reach 100% without any critical Damage Effects occurring. Beyond this point, additional damage point does not increase the chance of sinking, though certain Damage Effects can still cause the ship to sink. You can think of this situation as a ship having nothing left to explode—it’s merely a flooded hull adrift at sea. Adding more holes to the above-water section does not contribute to sinking.
`)
}

if(!groupDestroyedPrompted && hasGroupDestroyed())
{
    groupDestroyedPrompted = true;

    msgBox(`
A group has been destroyed!

You can open the "Victory Status" dialog from the "Command" tab in the top bar. It will report the top group's losses and damage situation. Victory points are calculated based on a ship's damage state, firepower, DP, and armor. These values can be found in the Ship Class Editor (static values) and the Ship Log Editor (dynamic values). These values are also used by the AI. Sinking a ship applies a ×2 modifier.
`)

}