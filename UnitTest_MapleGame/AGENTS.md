# Runtime test ownership

Apply the repository's root `AGENTS.md` and the test-audit skill to this suite.

- Keep one primary test owner for each contract. `GameSessionHostTests` owns
  thread affinity, completion, cancellation and disposal; assembly-boundary tests
  own emitted dependency references.
- Test the UI family selected from available status-bar images. Version metadata
  selectors without production callers are obsolete.
- Map-generation tests must follow the live activation path. The native graphics
  suite owns transition, cancellation and mandatory activation failure behavior;
  do not reintroduce a helper that rolls back a committed generation.
- Check the current protocol owner and its history before retaining packet
  rejection cases. Intentional opcode expansion can invalidate old exclusions.
- Connected-session fixtures must observe packet delivery or deferred scheduling,
  rather than only asserting connection flags supplied by the fixture.
- Preserve real-data, native lifecycle, storage schema and editor/runtime boundary
  coverage. Opt-in execution or private harness setup alone is not a reason to
  delete a test.
