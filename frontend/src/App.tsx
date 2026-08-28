import { useEffect, useState } from "react";
import { fetchRecentReviews } from "./api";
import { ReviewList } from "./components/ReviewList";
import { RunReviewForm } from "./components/RunReviewForm";
import type { ReviewReport } from "./types";
import "./index.css";

function App() {
  const [reviews, setReviews] = useState<ReviewReport[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    fetchRecentReviews()
      .then((data) => {
        if (!cancelled) setReviews(data);
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div id="dashboard">
      <header className="app-header">
        <h1>AI-Augmented Code Review Dashboard</h1>
        <p>Recent pull request reviews combining SonarQube static analysis with LLM requirement checking.</p>
      </header>

      <RunReviewForm
        onComplete={(report) => {
          // Put the new report at the top rather than refetching: a re-run of the same
          // pull request replaces the previous entry so the list does not accumulate
          // duplicates of one PR while the wording is being iterated on.
          setReviews((current) => [
            report,
            ...current.filter(
              (r) =>
                !(
                  r.owner === report.owner &&
                  r.repository === report.repository &&
                  r.pullRequestNumber === report.pullRequestNumber
                ),
            ),
          ]);
          setError(null);
        }}
      />

      {loading && <p>Loading reviews…</p>}
      {error && (
        <p className="error-state">
          Could not reach the backend API ({error}). Confirm the .NET API is running and VITE_API_BASE_URL is correct.
        </p>
      )}
      {!loading && !error && <ReviewList reviews={reviews} />}
    </div>
  );
}

export default App;
