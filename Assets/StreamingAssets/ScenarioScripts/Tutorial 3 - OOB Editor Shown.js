if(phase === Phase.WaitForOrderOfBattleShown)
{
    msgBoxDelay(`
OOB denotes the historical Order of Battle (though not the case for the tutorial scenario). You can set doctrine, leader, and the OOB itself (by adding/removing ships or groups).

Doctrine can be set at the ship level or any OOB level. If a doctrine field is set to the default "inherited" value, it will inherit the value from its parent OOB. This allows you to set general doctrine at the top OOB level and more specific doctrine at lower levels.

The most important doctrine settings are those related to automation. By default, firing is automated, but movement (course changes) is not. You can try turning off "inherited" and enabling movement automation for a root group. Alternatively, you can manually control both groups to play in sandbox mode.

Manual firing is a somewhat advanced topic and will be covered later.

Confirm to close and advance time until the two ships are within firing range. (This can be controlled by AI, manually, or by maintaining their initial courses).
`, 0.3);

    phase = Phase.WaitForFiringExchangeStarted;
}