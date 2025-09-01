fireAny = false;
for(var shipLog of NavalGameState.Instance.shipLogsOnMap)
{
    log(shipLog.namedShip.name.GetMergedName())
    for(var btyStatus of shipLog.batteryStatus)
    {
        // log(btyStatus)
        for(var mnt of btyStatus.mountStatus)
        {
            log(mnt.firingTargetObjectId);
            fireAny =  fireAny ||  mnt.firingTargetObjectId !== null
        }
    }
}