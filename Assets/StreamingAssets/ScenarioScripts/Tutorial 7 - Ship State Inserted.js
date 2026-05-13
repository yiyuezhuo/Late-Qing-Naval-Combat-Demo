var tutorial7ShipCount = NavalGameState.Instance.shipLogs.Count;

if (tutorial7Phase === Tutorial7Phase.WaitingForFirstShipInsertion && tutorial7ShipCount >= 1) {
    msgBox(`
You have already added a Named Ship Yoshino to Blue Ship Group.

Click Insert again to open the insert dialog, you will notice Yoshino is no longer available in the Named Ship column. However it remains available in the Ship Class columns.

Insert two more "anonymous" Yoshino by Ship Class method.
`);

    tutorial7Phase = Tutorial7Phase.WaitingForThirdShipInsertion;
}
else if (tutorial7Phase === Tutorial7Phase.WaitingForThirdShipInsertion && tutorial7ShipCount >= 3) {
    msgBox(`
The two new "anonymous" Yoshino objects should be named something like "Yoshino1" and "Yoshino2".

Select Yoshino1, click F or Follow button in the Top Tab and click on the Yoshino to set it to follow Yoshino. Then set Yoshino2 to follow Yoshino1. Set the desired speed of Yoshino (group leader) to 10 knots and heading to 90 degree (east). And click "Set to Formation Position" button in the Editor tab (if the button is not enabled, enable "Edit mode" in the Command tab). Position, speed and heading of ships would would be updated according to their formation relationships.
`);

    tutorial7Phase = Tutorial7Phase.WaitingForFormationPosition;
}
