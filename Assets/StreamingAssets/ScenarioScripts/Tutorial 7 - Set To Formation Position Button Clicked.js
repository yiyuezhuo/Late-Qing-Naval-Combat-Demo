if (tutorial7Phase === Tutorial7Phase.WaitingForFormationPosition) {
    msgBox(`
Now insert two other ships (Ex. China ships Chi Yuan and Kuang Yi), set Attach to Group to Red and click "Set to Formation Position" to arrange them. 

After that you have made a minimal scenario/sandbox and you can play it. 

Also you can save the scenario: Switch to File Tabs, disable "Save without Streaming Asset" (required because some anonymous Named Ship were created) and click the "Save (Edit)" button to save the scenario.

"Load Game" button in the Main Menu can be used to load the saved scenario to start to playing your new created custom scenario. 

You can also experiment with some other editing, like:

- Create/Delete group in the OOB editor
- Drag Ship/Group in the OOB Editor to change OOB relationship

Once you feel you have tested enough, exit this tutorial and move on to the next one.
`);

    tutorial7Phase = Tutorial7Phase.End;
}
