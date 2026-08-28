import type { ReviewReport } from "../types";

function severityClass(severity: string): string {
  return `badge badge-${severity.toLowerCase()}`;
}

export function ReviewList({ reviews }: { reviews: ReviewReport[] }) {
  if (reviews.length === 0) {
    return <p className="empty-state">No reviews yet. Open a pull request on a connected repository to see results here.</p>;
  }

  return (
    <div className="review-list">
      {reviews.map((review) => (
        <article className="review-card" key={`${review.owner}/${review.repository}#${review.pullRequestNumber}-${review.headSha}`}>
          <header>
            <h3>
              {review.owner}/{review.repository} #{review.pullRequestNumber}
            </h3>
            <time dateTime={review.generatedAtUtc}>{new Date(review.generatedAtUtc).toLocaleString()}</time>
          </header>

          <p className="ticket-source">
            {review.ticketSource === "ManualOverride"
              ? "Ticket supplied manually"
              : review.ticketSource === "GitHubIssue"
                ? "Ticket from linked GitHub issue"
                : "No ticket linked"}
            {review.ticketUrl && (
              <>
                {" — "}
                <a href={review.ticketUrl} target="_blank" rel="noreferrer">
                  ticket
                </a>
              </>
            )}
            {review.modelName && <span className="model-tag">{review.modelName}</span>}
          </p>

          <p className="summary">{review.summary}</p>

          {review.requirementCoverage.length > 0 && (
            <ul className="requirement-list">
              {review.requirementCoverage.map((item, idx) => (
                <li key={idx}>
                  <span>{item.covered ? "✅" : "⚠️"}</span> {item.requirement}
                </li>
              ))}
            </ul>
          )}

          {review.findings.length > 0 && (
            <table className="findings-table">
              <thead>
                <tr>
                  <th>Severity</th>
                  <th>Category</th>
                  <th>Location</th>
                  <th>Message</th>
                </tr>
              </thead>
              <tbody>
                {review.findings.map((finding, idx) => (
                  <tr key={idx}>
                    <td><span className={severityClass(finding.severity)}>{finding.severity}</span></td>
                    <td>{finding.category}</td>
                    <td>{finding.filePath ? `${finding.filePath}${finding.line ? `:${finding.line}` : ""}` : "-"}</td>
                    <td>{finding.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </article>
      ))}
    </div>
  );
}
