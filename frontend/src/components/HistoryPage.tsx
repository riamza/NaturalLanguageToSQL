import { useState } from "react";
import { History, MessageSquare, X, Play } from "lucide-react";
import type { QueryHistoryItem } from "../types";
import { ExpandableText } from "./ExpandableText";

interface HistoryPageProps {
  history: QueryHistoryItem[];
  onHistoryClick: (prompt: string) => void;
}

export function HistoryPage({ history, onHistoryClick }: HistoryPageProps) {
  const [selectedItem, setSelectedItem] = useState<QueryHistoryItem | null>(
    null,
  );

  return (
    <div className="page-container fade-in">
      <div className="page-header">
        <h1>
          <History className="page-icon" /> Query History
        </h1>
        <p className="page-description">
          Review past natural language queries, their resulting SQL, and
          performance metrics.
        </p>
      </div>

      <div className="history-grid">
        {history.length === 0 ? (
          <div className="empty-state">
            <History size={48} className="empty-icon" />
            <p>No queries found in your history.</p>
          </div>
        ) : (
          history.map((item) => (
            <div
              key={item.id}
              className="history-card"
              onClick={() => setSelectedItem(item)}
            >
              <div className="history-card-header">
                <div className="history-card-prompt">
                  <MessageSquare size={16} />
                  <ExpandableText text={item.prompt} maxLines={10} />
                </div>
                <div
                  className={`status-badge ${item.isSuccessful ? "success" : "error"}`}
                >
                  {item.isSuccessful ? "Success" : "Failed"}
                </div>
              </div>
              <div
                className="history-card-message"
                style={{
                  margin: "10px 0",
                  fontSize: "0.9rem",
                  color: item.isSuccessful ? "green" : "red",
                }}
              >
                <ExpandableText
                  text={
                    item.isSuccessful
                      ? "Query executed successfully."
                      : item.errorMessage || "Unknown error"
                  }
                  maxLines={10}
                />
              </div>
              <div className="history-card-footer">
                <span className="time">
                  {new Date(item.createdAtUtc).toLocaleString()}
                </span>
                <span className="duration">
                  {(item.executionTimeMs / 1000).toFixed(2)}s
                </span>
              </div>
            </div>
          ))
        )}
      </div>

      {selectedItem && (
        <div className="modal-overlay" onClick={() => setSelectedItem(null)}>
          <div
            className="modal-content premium-modal"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-header">
              <h3>
                <History size={18} />
                Query Details
              </h3>
              <button
                className="close-modal"
                onClick={() => setSelectedItem(null)}
                aria-label="Close"
              >
                <X size={20} />
              </button>
            </div>

            <div className="modal-body">
              <div className="modal-section">
                <span className="modal-section-title">
                  Natural Language Prompt
                </span>
                <div className="modal-text-block highlight-block">
                  <ExpandableText text={selectedItem.prompt} maxLines={10} />
                </div>
              </div>

              {selectedItem.generatedSql && (
                <div className="modal-section">
                  <span className="modal-section-title">Generated SQL</span>
                  <pre className="modal-code-block">
                    <code>
                      <ExpandableText
                        text={selectedItem.generatedSql}
                        maxLines={10}
                      />
                    </code>
                  </pre>
                </div>
              )}

              {selectedItem.errorMessage ? (
                <div className="modal-section">
                  <span className="modal-section-title">Error Message</span>
                  <div className="error-block">
                    <ExpandableText
                      text={selectedItem.errorMessage}
                      maxLines={10}
                    />
                  </div>
                </div>
              ) : (
                <div className="modal-section">
                  <span className="modal-section-title">Status</span>
                  <div
                    style={{
                      color: "green",
                      background: "rgba(0,128,0,0.1)",
                      padding: "10px",
                      borderRadius: "8px",
                    }}
                  >
                    <ExpandableText
                      text="Query executed successfully."
                      maxLines={10}
                    />
                  </div>
                </div>
              )}
            </div>

            <div className="modal-footer">
              <button
                className="btn-primary"
                onClick={() => {
                  onHistoryClick(selectedItem.prompt);
                  setSelectedItem(null);
                }}
              >
                <Play size={16} />
                Run again
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
