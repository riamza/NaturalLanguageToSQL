import { Download, ThumbsUp, ThumbsDown } from "lucide-react";

interface ResultsTableProps {
  results: any[];
  feedback: "up" | "down" | null;
  onFeedback: (vote: "up" | "down") => void;
  onExport: () => void;
}

export function ResultsTable({
  results,
  feedback,
  onFeedback,
  onExport,
}: ResultsTableProps) {
  if (results.length === 0) return null;

  return (
    <div className="results-wrapper">
      <div className="results-actions">
        <span className="results-count">{results.length} row(s) returned</span>
        <div className="actions-right">
          <div className="feedback-actions">
            <button
              type="button"
              className={`btn-icon ${feedback === "up" ? "active" : ""}`}
              onClick={() => onFeedback("up")}
              title="Good result"
            >
              <ThumbsUp size={16} />
            </button>
            <button
              type="button"
              className={`btn-icon ${feedback === "down" ? "active" : ""}`}
              onClick={() => onFeedback("down")}
              title="Bad result"
            >
              <ThumbsDown size={16} />
            </button>
          </div>
          <button type="button" className="btn-action" onClick={onExport}>
            <Download size={16} />
            Export CSV
          </button>
        </div>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              {Object.keys(results[0]).map((key) => (
                <th key={key}>{key}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {results.map((row, index) => (
              <tr key={index}>
                {Object.values(row).map((val: any, idx) => (
                  <td key={idx}>{val !== null ? String(val) : "NULL"}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
