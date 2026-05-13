var Tutorial7Phase = {
    WaitingForFirstInsertDialog: 1,
    WaitingForFirstShipInsertion: 2,
    WaitingForThirdShipInsertion: 3,
    WaitingForFormationPosition: 4,
    End: 5
};

var tutorial7Phase = Tutorial7Phase.WaitingForFirstInsertDialog;

msgBox(`
Welcome to Tutorial 7: Custom Scenarios. Here, you will learn how to build a custom scenario. What you are currently seeing is an almost blank scenario state - not completely blank, because the two default Red and Blue Ship Groups have already been created for convenience. This is also the state you enter from the main menu by using "Start As Empty." When you want to create a custom scenario, or start fighting in a temporary sandbox, you can also use "Start As Empty" to enter this state.

Press the Insert key, or the corresponding button in Top Tab - Editor, then click anywhere on the map to insert a unit.
`);
