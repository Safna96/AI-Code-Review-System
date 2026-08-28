import { useState } from "react";
import type { ReviewReport } from "../types";

/**
 * Severity is a status scale, not a series scale, so each level ships with a shape
 * and a word as well as a colour - the colour never carries the meaning on its own.
 */
const SEVERITY_ICON: Record<string, string> = {
  Critical: "▲",
  Major: "◆",
  Minor: "●",
};

function severityClass(severity: string): string {
  return `badge badge-${severity.toLowerCase()}`;
}

/** The most serious level present, used for the card's accent stripe. */
function worstSeverity(review: ReviewReport): string {
  if (review.criticalCount > 0) return "critical";
  if (review.majorCount > 0) return "major";
  if (review.minorCount > 0) return "minor";
  return "clean";
}

function prKey(review: ReviewReport): string {
  return `${review.owner}/${review.repository}#${review.pullRequestNumber}`;
}

function prUrl(review: ReviewReport): string {
  return `https://github.com/${review.owner}/${review.repository}/pull/${review.pullRequestNumber}`;
}

function ticketLabel(review: ReviewReport): string {
  if (review.ticketSource === "ManualOverride") return "Ticket supplied manually";
  if (review.ticketSource === "GitHubIssue") return "Ticket from linked issue";
  // Runs recorded before provenance was tracked default to "None". Saying "no ticket
  // linked" about a review that clearly assessed requirements would be wrong, so
  // distinguish "we know there was none" from "we did not record it".
  if (review.requirementCoverage.length > 0) return "Ticket source not recorded";
  return "No ticket linked";
}

/**
 * The headline numbers for one review, as tiles rather than a chart - four counts
 * have no shape worth plotting, and the job here is "read the state at a glance".
 */
function StatRow({ review }: { review: ReviewReport }) {
  const covered = review.requirementCoverage.filter((r) => r.covered).length;
  const total = review.requirementCoverage.length;

  return (
    <div className="stat-row">
      <div className="stat stat-coverage">
        <span className="stat-value">
          {total === 0 ? "—" : `${covered}/${total}`}
        </span>
        <span className="stat-label">Requirements met</span>
        {total > 0 && (
          <span
            className="coverage-bar"
            role="img"
            aria-label={`${covered} of ${total} requirements met`}
          >
            <span
              className="coverage-fill"
              style={{ width: `${Math.round((covered / total) * 100)}%` }}
            />
          </span>
        )}
      </div>
      <div className="stat stat-critical">
        <span className="stat-value">
          <span className="dot dot-critical" aria-hidden="true">{SEVERITY_ICON.Critical}</span>
          {review.criticalCount}
        </span>
        <span className="stat-label">Critical</span>
      </div>
      <div className="stat stat-major">
        <span className="stat-value">
          <span className="dot dot-major" aria-hidden="true">{SEVERITY_ICON.Major}</span>
          {review.majorCount}
        </span>
        <span className="stat-label">Major</span>
      </div>
      <div className="stat stat-minor">
        <span className="stat-value">
          <span className="dot dot-minor" aria-hidden="true">{SEVERITY_ICON.Minor}</span>
          {review.minorCount}
        </span>
        <span className="stat-label">Minor</span>
      </div>
    </div>
  );
}

function ReviewDetail({ review }: { review: ReviewReport }) {
  return (
    <>
      <StatRow review={review} />

      <p className="summary">{review.summary}</p>

      {review.requirementCoverage.length > 0 && (
        <section className="review-section">
          <h4>Requirement coverage</h4>
          <ul className="requirement-list">
            {review.requirementCoverage.map((item, idx) => (
              <li key={idx} className={item.covered ? "req-met" : "req-unmet"}>
                <span className="req-icon" aria-hidden="true">{item.covered ? "✓" : "!"}</span>
                <span className="req-text">
                  {item.requirement}
                  {item.evidence && <em className="req-evidence">{item.evidence}</em>}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {review.findings.length > 0 && (
        <section className="review-section">
          <h4>
            Findings <span className="count-pill">{review.findings.length}</span>
          </h4>
          <table className="findings-table">
            <thead>
              <tr>
                <th>Severity</th>
                <th>Source</th>
                <th>Location</th>
                <th>Message</th>
              </tr>
            </thead>
            <tbody>
              {review.findings.map((finding, idx) => (
                <tr key={idx}>
                  <td>
                    <span className={severityClass(finding.severity)}>
                      <span aria-hidden="true">{SEVERITY_ICON[finding.severity]}</span> {finding.severity}
                    </span>
                  </td>
                  <td>
                    <span className={`source-tag source-${finding.source === "StaticAnalysis" ? "sonar" : "llm"}`}>
                      {finding.source === "StaticAnalysis" ? "SonarQube" : "LLM"}
                    </span>
                  </td>
                  <td className="cell-location">
                    {finding.filePath
                      ? `${finding.filePath}${finding.line ? `:${finding.line}` : ""}`
                      : "—"}
                  </td>
                  <td>{finding.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </>
  );
}

/** One earlier run of a pull request, collapsed until asked for. */
function HistoryRow({ review }: { review: ReviewReport }) {
  const [open, setOpen] = useState(false);
  const covered = review.requirementCoverage.filter((r) => r.covered).length;
  const total = review.requirementCoverage.length;

  return (
    <div className={`history-row${open ? " is-open" : ""}`}>
      <button type="button" className="history-toggle" onClick={() => setOpen((v) => !v)}>
        <span className="history-caret" aria-hidden="true">{open ? "▾" : "▸"}</span>
        <time dateTime={review.generatedAtUtc}>
          {new Date(review.generatedAtUtc).toLocaleString()}
        </time>
        <span className="history-meta">
          {total > 0 ? `${covered}/${total} met` : "no ticket"} · {review.findings.length} findings
          {review.modelName && <> · <code>{review.modelName}</code></>}
        </span>
      </button>
      {open && (
        <div className="history-body">
          <ReviewDetail review={review} />
        </div>
      )}
    </div>
  );
}

export function ReviewList({ reviews }: { reviews: ReviewReport[] }) {
  if (reviews.length === 0) {
    return (
      <p className="empty-state">
        No reviews yet. Open a pull request on a connected repository, or run one above.
      </p>
    );
  }

  // Group runs by pull request so re-running the same PR builds a history under it
  // rather than filling the page with near-identical cards. Earlier runs are kept,
  // not discarded - comparing them is the point when the ticket wording is varied.
  const groups = new Map<string, ReviewReport[]>();
  for (const review of reviews) {
    const key = prKey(review);
    const existing = groups.get(key);
    if (existing) existing.push(review);
    else groups.set(key, [review]);
  }

  const ordered = [...groups.values()]
    .map((runs) =>
      [...runs].sort(
        (a, b) => new Date(b.generatedAtUtc).getTime() - new Date(a.generatedAtUtc).getTime(),
      ),
    )
    .sort(
      (a, b) => new Date(b[0].generatedAtUtc).getTime() - new Date(a[0].generatedAtUtc).getTime(),
    );

  return (
    <div className="review-list">
      {ordered.map((runs) => {
        const latest = runs[0];
        const older = runs.slice(1);

        return (
          <article className={`review-card accent-${worstSeverity(latest)}`} key={prKey(latest)}>
            <header className="review-head">
              <div className="review-title">
                <h3>
                  <a href={prUrl(latest)} target="_blank" rel="noreferrer">
                    {latest.owner}/{latest.repository}
                    <span className="pr-number"> #{latest.pullRequestNumber}</span>
                  </a>
                </h3>
                <time dateTime={latest.generatedAtUtc}>
                  {new Date(latest.generatedAtUtc).toLocaleString()}
                </time>
              </div>
              <div className="review-tags">
                <span className={`tag tag-${latest.ticketSource === "ManualOverride" ? "manual" : "plain"}`}>
                  {ticketLabel(latest)}
                </span>
                {latest.ticketUrl && (
                  <a className="tag tag-link" href={latest.ticketUrl} target="_blank" rel="noreferrer">
                    ticket ↗
                  </a>
                )}
                {latest.modelName && <span className="tag tag-model">{latest.modelName}</span>}
              </div>
            </header>

            <ReviewDetail review={latest} />

            {older.length > 0 && (
              <section className="history">
                <h4 className="history-heading">
                  Earlier runs <span className="count-pill">{older.length}</span>
                </h4>
                {older.map((run) => (
                  <HistoryRow key={`${run.headSha}-${run.generatedAtUtc}`} review={run} />
                ))}
              </section>
            )}
          </article>
        );
      })}
    </div>
  );
}
