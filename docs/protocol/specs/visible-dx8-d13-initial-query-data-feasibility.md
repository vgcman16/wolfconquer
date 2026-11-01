# Initial query feasibility and startup spell succession

Status: OPEN source implementation; author has not built or run it.
Target: VisibleDx8D13. Successful query and startup packet layouts are unchanged.

The actual ordinary admission lease produces its detached final candidate
before journal/native publication. `InitialWorldEntryQueryData` receives that
exact candidate, the Frame's configured catalog and actual clock. It performs
the same HP phase replay, checked native magic/mana aggregation, vigor bounds,
derived reconciliation and output serialization as bound query preflight.
The real InitialWorldEntry admission decision is repeated by currentness, so
the signed FineRain clock guard is not assumed independent of time. The DATA
decision does not publish a stamp, native cache or successful query.

The closed `AdmissionQueryData` wrapper reuses the existing collectors for
actually selected catalog rows and candidate inputs. Its internal collector
has no binding and is never returned as a live witness. Live metadata
`IsCurrentPlain` still requires the actual bound Store/native owner. The DATA
wrapper pins candidate/catalog identity and retained raw maps, items, spell
rows, gear and used metadata contents. It provides no operation lease, epoch,
file permit or completion. The actual bound query preparation remains required
after admission publishes.

The interserver JiangHu branch depends on historical native cache state and
refuses this DATA preparation without its real owner. No zero cache is invented.
`RequiresDerivedSpellWrite` reports a calculated requirement only; the real
admission plan must separately check configured Character/Items/Spells roots
and recovery readiness before its commit and recheck them at publication.

The initial query can legitimately change derived spell membership before
the later startup grant packet slot. A startup spell-publication owner therefore
advances only through the actual `CompletedAdmissionContinuationQuery`. The
receipt must name its exact pre-query holder, and the successor retains the
same original Store/native identity. Its full-book plan matches the completed
query's new book while retaining identical original selected grant rows and
frame bytes. Unchanged frames are sent only at the existing startup slot; a
second query does not add another grant send. If a selected startup grant row
would itself change, `PreservesLoginPublication` refuses that known case during
detached DATA preparation instead of blessing stale output.

DATA tests cover exact candidate identity, opaque case variants, interserver
and numeric refusal, clock decision rechecks without stamp publication, and
startup selection preservation/corruption. They do not establish real admission,
query completion, live grant authority or restart behavior. Team owns the actual
admission plan integration; Root owns combined qualification and activation.
