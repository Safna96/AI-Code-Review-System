import type { ReviewReport, RunReviewRequest } from "./types";

// Points at the .NET backend from Table 1 of the proposal (ASP.NET Core Web API).
// Override with VITE_API_BASE_URL in frontend/.env for non-local deployments.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:8080";

export async function fetchRecentReviews(): Promise<ReviewReport[]> {
  const response = await fetch(`${API_BASE_URL}/api/reviews`);
  if (!response.ok) {
    throw new Error(`Failed to load reviews: ${response.status} ${response.statusText}`);
  }
  return response.json();
}

/**
 * Runs a review on demand. This is the evaluation path, not the normal one - in
 * ordinary use the review is triggered by the GitHub webhook when a pull request
 * is opened, with nobody typing anything here.
 *
 * The request is slow by nature (the LLM call dominates), so callers should expect
 * tens of seconds rather than a snappy response.
 */
export async function runReview(request: RunReviewRequest): Promise<ReviewReport> {
  const response = await fetch(`${API_BASE_URL}/api/reviews/run`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    // The API returns { message } for both bad input (400) and upstream failures (502).
    const detail = await response.json().catch(() => null);
    throw new Error(detail?.message ?? `Review failed: ${response.status} ${response.statusText}`);
  }

  return response.json();
}
