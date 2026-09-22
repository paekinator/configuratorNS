# Parts and temporary prices

Open `Assets/Scenes/ConfiguratorScene.unity` in Unity, select **BuildStats** in the Hierarchy, and edit its **UI Build Stats** component in the Inspector. Save the scene while outside Play Mode, then rebuild to distribute price changes.

The temporary AUD unit estimates are **Panel Price: 25**, **Veneer Price: 5**, **Cap Side Price: 2**, **Cap End Price: 2**, and **Foot Price: 5**. Each is per installed part, regardless of size. These are arbitrary placeholders, not supplier prices. Existing frame estimates are retained.

For individual sizes, add an entry to **Part Prices** on that same component. Use the exact ID from the Parts & prices list, for example `Panel H7xH3`, `Veneer H7`, `Cap Side`, `Cap End`, or `Foot`. An individual entry overrides the category default. Panel IDs list the larger size first. Custom panel cuts are listed by measured millimetres. Unit prices round to two decimal places before calculating line totals. Zero means explicitly free; a negative or invalid value is shown as **PRICE REQUIRED** and excluded from the estimated subtotal.

Click the parts/price header, or choose **Parts & prices** in the menu, to see frame, panel and finish quantities with unit prices and totals. **Prepare quote** in Piece Mode exports the same inventory and the design code. The estimate excludes delivery, installation, unlisted accessories and tax adjustments.

Finish quantities count the actual active generated model objects, including each veneer size, caps and feet. Removing Finish removes those quantities and their prices. Panels count separately by size. Ghost previews and inactive replaced parts are excluded. Space Mode uses the currently visible merge results instead of old saved-piece prices.

New and updated saved pieces include finish quantities. Existing saved cards retain their saved metadata; open and update a piece to refresh its count and estimate. Shared design codes continue to store the design and Finish choice, with the finish inventory regenerated when opened.
