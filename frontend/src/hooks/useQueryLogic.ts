import { useState } from "react";
import axios from "axios";
import { toast } from "sonner";
import type { QueryResponse } from "../types";

const backendUrl = import.meta.env.VITE_API_URL || "http://localhost:5071";

export function useQueryLogic(onSuccess?: () => void) {
  const [prompt, setPrompt] = useState("");
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<any[]>([]);
  const [generatedSql, setGeneratedSql] = useState("");
  const [error, setError] = useState("");
  const [executionTime, setExecutionTime] = useState<number | null>(null);
  const [rowsAffected, setRowsAffected] = useState<number | null>(null);
  const [feedback, setFeedback] = useState<"up" | "down" | null>(null);
  const [currentHistoryId, setCurrentHistoryId] = useState<string | null>(null);
  const [pendingApproval, setPendingApproval] = useState<{
    sql: string;
    ir: any;
    originalPrompt?: string;
  } | null>(null);

  const handleError = (err: any, defaultMsg: string) => {
    let errorMsg = defaultMsg;
    if (err.response?.data) {
      if (typeof err.response.data === "string") {
        errorMsg = err.response.data;
      } else if (typeof err.response.data === "object") {
        errorMsg =
          err.response.data.message ||
          err.response.data.title ||
          JSON.stringify(err.response.data);
      }
    } else if (err.message) {
      errorMsg = err.message;
    }
    setError(errorMsg);
  };

  const executeSearch = async (query: string) => {
    if (!query.trim()) return;

    setPrompt(query);
    setLoading(true);
    setError("");
    setResults([]);
    setGeneratedSql("");
    setExecutionTime(null);
    setRowsAffected(null);
    setFeedback(null);
    setCurrentHistoryId(null);
    setPendingApproval(null);

    try {
      const response = await axios.post<QueryResponse>(
        `${backendUrl}/api/query/ask`,
        { prompt: query }
      );

      if (response.data.requiresApproval) {
        setPendingApproval({
          sql: response.data.generatedSql,
          ir: response.data.ir,
          originalPrompt: response.data.originalPrompt,
        });
      } else {
        setResults(response.data.data);
        setGeneratedSql(response.data.generatedSql);
        setExecutionTime(response.data.executionTimeMs);
        setCurrentHistoryId(response.data.historyId);
      }

      if (onSuccess) onSuccess();
    } catch (err: any) {
      handleError(err, "An error occurred.");
      if (onSuccess) onSuccess();
    } finally {
      setLoading(false);
    }
  };

  const handleSearchClick = async (e?: React.FormEvent, overridePrompt?: string) => {
    if (e) e.preventDefault();
    await executeSearch(overridePrompt || prompt);
  };

  const handleApprove = async () => {
    if (!pendingApproval) return;
    setLoading(true);
    setError("");

    try {
      const response = await axios.post(
        `${backendUrl}/api/query/execute-approved`,
        {
          ir: pendingApproval.ir,
          originalPrompt: pendingApproval.originalPrompt,
        }
      );

      setGeneratedSql(response.data.generatedSql);
      setExecutionTime(response.data.executionTimeMs);
      setRowsAffected(response.data.rowsAffected);
      setCurrentHistoryId(response.data.historyId);
      setPendingApproval(null);
      setPrompt("");
      toast.success("Date inserate cu succes!");

      if (onSuccess) onSuccess();
    } catch (err: any) {
      handleError(err, "An error occurred during execution.");
    } finally {
      setLoading(false);
    }
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.endsWith(".csv")) {
      setError("Please upload a valid .csv file.");
      return;
    }

    const reader = new FileReader();
    reader.onload = async (event) => {
      const csvText = event.target?.result as string;
      if (!csvText) return;

      const promptText = `Insert this data into the correct table based on these columns:\n\n${csvText}`;
      setPrompt(promptText);
      await executeSearch(promptText);
    };
    reader.onerror = () => setError("Failed to read the uploaded CSV file.");
    reader.readAsText(file);
  };

  const setFeedbackVote = (vote: "up" | "down") => {
      setFeedback(vote);
  }

  return {
    prompt,
    setPrompt,
    loading,
    results,
    generatedSql,
    error,
    executionTime,
    rowsAffected,
    feedback,
    setFeedback: setFeedbackVote,
    currentHistoryId,
    pendingApproval,
    setPendingApproval,
    handleSearch: handleSearchClick,
    handleApprove,
    handleFileUpload,
  };
}
