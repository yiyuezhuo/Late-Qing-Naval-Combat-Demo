var ncc = importNamespace('NavalCombatCore');
var ns = importNamespace("");

var damageEffectId = "164";

for(var shipLog of NavalGameState.Instance.shipLogsOnMap)
{
    log("Applying to" + shipLog.namedShip.name.GetMergedName())

    var ctx = new ncc.DamageEffectContext();
    ctx.subject = shipLog;
    ctx.baseDamagePoint = 11;
    ctx.hitPenDetType = ncc.HitPenDetType.PenetrateWithDetonate;
    ctx.ammunitionType = ncc.AmmunitionType.ArmorPiercing;
    ctx.shellDiameterInch = 10;
    ctx.addtionalDamageEffectProbility = 1;

    ncc.DamageEffectChart.AddNewDamageEffect(ctx, damageEffectId);
    // ncc.DamageEffectChart.AddNewDamageEffect(ctx);
}