if (tutorial7Phase === Tutorial7Phase.WaitingForFirstInsertDialog) {
    msgBox(`
After selecting a position in insert mode, the Insert Dialog will be displayed.

You can insert three types of objects here:

- **Not Deployed Ship State**: Deploy a not deployed ship (the ship is usually created manually in the Ship State Editor). This is a somewhat advanced topic and is not covered in this tutorial.
- **Named Ship**: This create a Ship State from the selected Named Ship which is not associated with a Ship State (so that a Named Ship can only has a Ship State "embodiment") with proper initialization, and deploys it to map.
- **Ship Class**: This creates an "anonymous" Named Ship from the selected Ship Class and create a Ship State from that new Named Ship. It is used for hypothetical scenarios involving ship classes that exceed historical quantities. Some historical information from the built-in Named Ship is lost though.

Select Named Ship Yoshino (the first item in the Named Ship column), choose Blue in the "Attach to Group" Dropdown, and click the Confirm button.
`);

    tutorial7Phase = Tutorial7Phase.WaitingForFirstShipInsertion;
}
