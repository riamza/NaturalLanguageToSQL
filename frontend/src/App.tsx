import { useEffect } from "react";
import { Toaster } from "sonner";
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

import { useHistory } from "./hooks/useHistory";
import { useQueryLogic } from "./hooks/useQueryLogic";

function App() {
  const navigate = useNavigate();
  const location = useLocation();

  const { history, fetchHistory, submitFeedback } = useHistory();
  
  const {
    prompt,
    setPrompt,
    loading,
    results,
    generatedSql,
    error,
    executionTime,
    rowsAffected,
    feedback,
    setFeedback,
    pendingApproval,
    setPendingApproval,
    handleSearch,
    handleApprove,
    handleFileUpload,
  } = useQueryLogic(fetchHistory);

  useEffect(() => {
    fetchHistory();
  }, [fetchHistory]);

  const exportCSV = () => {
    if (!results.length) return;
    const headers = Object.keys(results[0]).join(",");
    const rows = results
      .map((row) =>
        Object.values(row)
          .map(
            (val) => `"${String(val !== null ? val : "").replace(/"/g, '""')}"`
          )
          .join(",")
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

  useEffect(() => {
    if (location.pathname === "/metrics" || location.pathname === "/history") {
      fetchHistory();
    }
  }, [location.pathname, fetchHistory]);

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
                    onFeedback={(vote) => {
                      setFeedback(vote);
                      if (history.length > 0) {
                         submitFeedback(history[0].id, vote);
                      }
                    }}
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
