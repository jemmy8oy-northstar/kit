/**
 * BEH-GATE-H2: what is typed here is saved to the project's repository, which may be public
 * (BEH-GATE-H1, kit#795), so the box says so before anything is sent. Point a textarea's
 * `aria-describedby` at `id`.
 */
export default function PublishedHint({ id }: { id: string }) {
  return (
    <p id={id} className="muted">
      Saved to this project's GitHub repository. If that repository is public, anyone can read it.
    </p>
  )
}
