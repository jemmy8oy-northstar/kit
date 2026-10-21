/**
 * BEH-GATE-H2: what is typed here is saved to Kit's repository, which is public (BEH-GATE-H1), so the
 * box says so before anything is sent. Point a textarea's `aria-describedby` at `id`.
 */
export default function PublishedHint({ id }: { id: string }) {
  return (
    <p id={id} className="muted">
      Public: this is saved to Kit's GitHub repository, which anyone can read.
    </p>
  )
}
