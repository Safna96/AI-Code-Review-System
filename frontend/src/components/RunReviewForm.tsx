import { useState } from "react";
import { runReview } from "../api";
import type { ReviewReport } from "../types";

/**
 * Manual run form, used for building the evaluation set.
 *
 * This is deliberately NOT the primary way to use the system: in normal operation
 * the GitHub webhook triggers a review when a pull request is opened and nobody
 * types anything here. The form exists so the same pull request can be reviewed
 * repeatedly under different ticket wordings, which is what makes the
 * requirement-coverage behaviour measurable.
 */
export function RunReviewForm({ onComplete }: { onComplete: (report: ReviewReport) => void }) {
  const [pullRequestUrl, setPullRequestUrl] = useState("");
  const [ticketDescription, setTicketDescription] = useState("");
  const [ticketUrl, setTicketUrl] = useState("");
  const [showTicketOverride, setShowTicketOverride] = useState(false);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setRunning(true);
    try {
      const report = await runReview({
        pullRequestUrl: pullRequestUrl.trim(),
        // Sent only when non-empty, so an untouched form keeps the default
        // behaviour of resolving "Closes #N" from the pull request body.
        ticketDescription: ticketDescription.trim() || undefined,
        ticketUrl: ticketUrl.trim() || undefined,
      });
      onComplete(report);
      setTicketDescription("");
      setTicketUrl("");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setRunning(false);
    }
  }

  return (
    <form className="run-form" onSubmit={handleSubmit}>
      <div className="run-form-heading">
        <h2>Run a review manually</h2>
        <span className="run-form-badge">evaluation tool</span>
      </div>
      <p className="run-form-note">
        Normally a review runs by itself when a pull request is opened. Use this to re-run one on
        demand, or to test how the review responds to differently worded requirements.
      </p>

      <label htmlFor="pr-url">Pull request URL</label>
      <input
        id="pr-url"
        type="url"
        required
        placeholder="https://github.com/owner/repo/pull/12"
        value={pullRequestUrl}
        onChange={(e) => setPullRequestUrl(e.target.value)}
        disabled={running}
      />

      <button
        type="button"
        className="link-button"
        onClick={() => setShowTicketOverride((v) => !v)}
        disabled={running}
      >
        {showTicketOverride ? "− Hide" : "+ Override"} the ticket description (optional)
      </button>

      {showTicketOverride && (
        <div className="run-form-override">
          <label htmlFor="ticket-text">Ticket description / acceptance criteria</label>
          <textarea
            id="ticket-text"
            rows={7}
            placeholder={
              "Leave empty to use the GitHub issue the PR links to with \"Closes #N\".\n\n" +
              "Paste requirements here to override it — e.g. the acceptance criteria from a Jira ticket."
            }
            value={ticketDescription}
            onChange={(e) => setTicketDescription(e.target.value)}
            disabled={running}
          />

          <label htmlFor="ticket-url">Ticket URL (optional)</label>
          <input
            id="ticket-url"
            type="url"
            placeholder="https://yourorg.atlassian.net/browse/PROJ-123"
            value={ticketUrl}
            onChange={(e) => setTicketUrl(e.target.value)}
            disabled={running}
          />
          <p className="run-form-hint">
            Recorded so you know which ticket the requirements came from. Nothing is fetched from
            this link — paste the criteria into the box above.
          </p>
        </div>
      )}

      <button type="submit" className="primary-button" disabled={running || !pullRequestUrl.trim()}>
        {running ? "Reviewing… (this takes 15–60 seconds)" : "Run review"}
      </button>

      {error && <p className="error-state">{error}</p>}
    </form>
  );
}
