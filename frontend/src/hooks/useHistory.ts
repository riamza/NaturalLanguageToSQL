import { useState, useCallback } from "react";
import axios from "axios";
import type { QueryHistoryItem } from "../types";

const backendUrl = import.meta.env.VITE_API_URL || "http://localhost:5071";

export function useHistory() {
  const [history, setHistory] = useState<QueryHistoryItem[]>([]);

  const fetchHistory = useCallback(async () => {
    try {
      const response = await axios.get<QueryHistoryItem[]>(
        `${backendUrl}/api/history`
      );
      setHistory(response.data);
    } catch (err) {
      console.error("Failed to load history", err);
    }
  }, []);

  const submitFeedback = useCallback(async (historyId: string, vote: "up" | "down") => {
    try {
      await axios.put(`${backendUrl}/api/history/${historyId}/feedback`, {
        feedback: vote,
      });
      await fetchHistory();
    } catch (err) {
      console.error("Failed to submit feedback", err);
    }
  }, [fetchHistory]);

  return {
    history,
    fetchHistory,
    submitFeedback,
  };
}
