import React, { useState, useEffect } from "react";
import axios from "axios";
import { Toaster, toast } from "sonner";
import { Routes, Route, useNavigate, useLocation } from "react-router-dom";
import "./App.css";

import { Sidebar } from "./components/Sidebar";
import { ErrorDisplay } from "./components/ErrorDisplay";
import { PendingApprovalPanel } from "./components/PendingApprovalPanel";
import { SqlPanel } from "./components/SqlPanel";
import { ResultsTable } from "./components/ResultsTable";
import { SearchForm } from "./components/SearchForm";
import { Header } from "./components/Header";

import { HistoryPage } from "./components/HistoryPage";
import { SchemaViewerPage } from "./components/SchemaViewerPage.tsx";
import { MetricsPage } from "./components/MetricsPage";

import type { QueryResponse, QueryHistoryItem } from "./types";

function App() {
  const navigate = useNavigate();
  const location = useLocation();
  const [prompt, setPrompt] = useState("");
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<any[]>([]);
  const [generatedSql, setGeneratedSql] = useState("");
  const [error, setError] = useState("");
  const [executionTime, setExecutionTime] = useState<number | null>(null);
  const [rowsAffected, setRowsAffected] = useState<number | null>(null);
  const [history, setHistory] = useState<QueryHistoryItem[]>([]);
  const [feedback, setFeedback] = useState<"up" | "down" | null>(null);
  const [currentHistoryId, setCurrentHistoryId] = useState<string | null>(null);
  const [pendingApproval, setPendingApproval] = useState<{
    sql: string;
    ir: any;
    originalPrompt?: string;
  } | null>(null);

  const backendUrl = import.meta.env.VITE_API_URL || "http://localhost:5071";

  const fetchHistory = async () => {
    try {
      const response = await axios.get<QueryHistoryItem[]>(
        `${backendUrl}/api/history`,
      );
      setHistory(response.data);
    } catch (err) {
      console.error("Failed to load history", err);
    }
  };

  useEffect(() => {
    fetchHistory();
  }, []);

  const handleSearch = async (e?: React.FormEvent, overridePrompt?: string) => {
    if (e) e.preventDefault();
    const query = overridePrompt || prompt;
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
        { prompt: query },
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

      await fetchHistory();
    } catch (err: any) {
      let errorMsg = "An error occurred.";
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
      await fetchHistory();
    } finally {
      setLoading(false);
    }
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
        },
      );

      setGeneratedSql(response.data.generatedSql);
      setExecutionTime(response.data.executionTimeMs);
      setRowsAffected(response.data.rowsAffected);
      setCurrentHistoryId(response.data.historyId);
      setPendingApproval(null);
      setPrompt("");
      toast.success("Date inserate cu succes!");

      await fetchHistory();
    } catch (err: any) {
      let errorMsg = "An error occurred during execution.";
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
    } finally {
      setLoading(false);
    }
  };

  const handleFeedback = async (vote: "up" | "down") => {
    if (!currentHistoryId) return;
    setFeedback(vote);
    try {
      await axios.put(`${backendUrl}/api/history/${currentHistoryId}/feedback`, {
        feedback: vote,
      });
      await fetchHistory();
    } catch (err) {
      console.error("Failed to submit feedback", err);
    }
  };

  const exportCSV = () => {
    if (!results.length) return;
    const headers = Object.keys(results[0]).join(",");
    const rows = results
      .map((row) =>
        Object.values(row)
          .map(
            (val) => `"${String(val !== null ? val : "").replace(/"/g, '""')}"`,
          )
          .join(","),
      )
      .join("\n");
    const blob = new Blob([`${headers}\n${rows}`], {
      type: "text/csv;charset=utf-8;",
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `query_export_${new Date().getTime()}.csv`;
    link.click();
    URL.revokeObjectURL(url);
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
      await handleSearch(undefined, promptText);
    };
    reader.onerror = () => setError("Failed to read the uploaded CSV file.");
    reader.readAsText(file);
  };

  // Make sure to refetch history when going to metrics
  useEffect(() => {
    if (location.pathname === "/metrics" || location.pathname === "/history") {
      fetchHistory();
    }
  }, [location.pathname]);

  return (
    <div className="app-layout">
      <Toaster position="bottom-right" richColors />
      <Sidebar 
        history={history}
        onHistoryClick={(p) => {
          setPrompt(p);
          navigate("/");
          handleSearch(undefined, p);
        }}
      />

      <main className="main-content">
        <div className="container">
          <Routes>
            <Route
              path="/"
              element={
                <>
                  <Header />

                  <SearchForm
                    prompt={prompt}
                    setPrompt={setPrompt}
                    loading={loading}
                    onSearch={handleSearch}
                    onFileUpload={handleFileUpload}
                  />

                  <ErrorDisplay
                    error={error}
                    prompt={prompt}
                    loading={loading}
                    onRetry={(p) => handleSearch(undefined, p)}
                  />

                  <PendingApprovalPanel
                    pendingApproval={pendingApproval}
                    loading={loading}
                    onApprove={handleApprove}
                    onCancel={() => setPendingApproval(null)}
                  />

                  {!pendingApproval && (
                    <SqlPanel
                      sql={generatedSql}
                      executionTime={executionTime}
                    />
                  )}

                  <ResultsTable
                    results={results}
                    feedback={feedback}
                    onFeedback={handleFeedback}
                    onExport={exportCSV}
                  />

                  {!loading &&
                    results.length === 0 &&
                    generatedSql &&
                    !error && (
                      <div className="no-results">
                        {rowsAffected !== null
                          ? `${rowsAffected} rânduri au fost afectate.`
                          : "No results found for your query."}
                      </div>
                    )}
                </>
              }
            />

            <Route
              path="/history"
              element={
                <HistoryPage
                  history={history}
                  onHistoryClick={(p) => {
                    setPrompt(p);
                    navigate("/");
                    handleSearch(undefined, p);
                  }}
                />
              }
            />

            <Route path="/schema" element={<SchemaViewerPage />} />

            <Route
              path="/metrics"
              element={<MetricsPage history={history} />}
            />
          </Routes>
        </div>
      </main>
    </div>
  );
}

export default App;
