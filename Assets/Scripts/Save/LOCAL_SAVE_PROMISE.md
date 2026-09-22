# Initial save and recovery promise (T-108 / T-109)

The first supported journey is an explicit **Save as piece / Update / Open**
on this device. A saved piece contains its name, geometry configuration code,
part counts, dimensions, finish on/off state, and optional thumbnail. Display
prices are estimates recorded at save time; reopening recalculates the live
summary from the current catalogue. Material swatch choices remain global
device preferences, not individual configuration-code fields.

Desktop saves live under Unity's `persistentDataPath/Pieces`. WebGL uses the
browser's local filesystem backed by IndexedDB. Browser save success appears
only after the IndexedDB flush callback succeeds. Quota, blocked storage, and
unconfirmed writes produce an error and expose the configuration code for an
independent backup. A write timeout does not mean the write succeeded.

Each JSON replacement is staged and validated; the last valid committed
revision is retained separately. Opening the library prefers a valid primary,
then a valid backup/backup staging file, then a validated initial-save staging
file. Recovery is disclosed in the piece row and status. Invalid records stay
on disk for recovery. A complete staged first save can be recovered after an
interruption; an incomplete first save cannot. Updates retain one previous
valid revision. Thumbnail failure does not invalidate geometry.

Loading verifies code integrity and required catalogue/panel assets before
mutation. It parks the actual previous scene objects until all requested
beams and panels have loaded. Partial loads restore those original objects.
A successful load creates one undo step; a rejected load creates none.

This is manual local saving. There is no cloud account, cross-device sync,
automatic draft save, or guarantee after browser/site-data clearing, private
session expiry, storage eviction, or device loss. Use **ID** and keep the code
outside this browser as the portable backup. Do not close a browser tab while
the UI reports that a save is still in progress.

Validation entry points: `PiecePersistenceSelfTest.RunAll` (filesystem recovery
states) and `ConfigurationRestoreRegression.RunAll` (Play Mode load, rollback,
and history). The ConfigCode editor self-test menu also runs persistence tests.
