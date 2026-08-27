import type { ReviewReport } from "./types";

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
