# Captured Character native-prefix preparation

Source base f483f98387b232e298ba54f205a5efb99dcb552e. This component prepares
the actual native Player, Beasts and Hairface participants from immutable SQL
Character DATA. It is not authenticated admission, a current Store/connection
lifetime, SQL revision/UID custody, a durable commit or a login. No caller is
wired and no publication/commit API is exposed by the captured-prefix wrapper.

`LegacyCapturedCharacterPrefix` is B's closed byte capture and parser. Its saved
Online value is DATA. The captured native preparation reuses the existing
ExistingPrefixLoad before/currentness/plain machinery and prepares native Online
as checked(actual-before.Online+1). The saved successor bytes and returned
PreparedSnapshot instead contain Online1. Those are separate existing meanings.

`LegacyNativeCapturedCharacterPrefix.TryPrepare(connection, captured, out prepared,
out reason)` captures actual Player/Beasts/Hairface before accessing metadata,
then uses the real native prepared participant classes. It retains those only
in private fields. `IsCurrent()` means preparation readiness only: matching
native objects, unchanged before-values and current referenced metadata. It
does not establish a genuine producer, authenticated account, Store lifetime or
SQL revision. Metadata/value rechecks are not a future transaction authority.
The actual transport producer's observed EOF also denies preparation/readiness
through a volatile, read-only HasObservedTransportClose check. Its false value
does not prove connectivity, ownership or authentication.

Exposed DATA is the immutable input Image, defensively copied PreparedSnapshot,
immutable PreparedPlayerValues and copied Online Character bytes. No mutable
native target, prepared participant, accepting callback or temporary file is
exposed. There is no publication, SQL mutation, success packet or file access on
the captured route. Missing native prerequisites, stale objects/catalogs, invalid
Beasts/Hairface or Online overflow refuse without publishing any native state.

Captured parsing uses full validated bytes, explicit quote/control limitations,
exact encoding roundtrip and exact Online-only replacement as specified in the
shared milestone. Captured Hairface uses the full bounded string and real strict
parser, avoiding the old file reader's254-character limit. Account identity is
positive Int64 and remains independent of native UInt32 Character UID.

## Existing file-prefix safety correction

The old optional INI readers silently converted malformed present bool/uint/
byte/ushort values to defaults, and consulted mutable global converter delegates.
That is an unsafe accepted positive, not compatibility to preserve. A concrete
LegacyNativeFilePrefixValues helper reads the real INI under the existing actual
file lease and uses fixed invariant parsing. Presence comes from the complete
strict baseline fields. Missing optional values keep their established defaults.
Recognized strings use bounded255 and256 reads to detect truncation; no raw-size
stack allocation is introduced. Genuine Win32 surrounding-quote semantics remain
in the file reader. Malformed/overflowing present typed values refuse before
Online replacement or native publication. Mutable conversion delegates are not
invoked after validation. Existing path, positive wire behavior and real prepared
native owners remain unchanged.
Raw NUL and embedded controls in the actual Win32-unquoted value also refuse
with normalPrefixFieldControlValue; ordinary boundary whitespace remains valid.

C owns retroactive malformed/numeric RelicSpirit refusal tests, valid missing/
quoted/whitespace controls, fixed-converter independence and genuine metadata/
stale/overflow cases. No test pass is claimed before Root runs them. All source
is initially unbuilt/unrun while the sole full gate owns f483. Future real SQL
admission still requires authentication, Store/revision/UID custody, complete
sidecar/startup/writer joins, recovery, protocol output and client acceptance.
