import { Clock } from "lucide-react";

interface SqlPanelProps {
  sql: string;
  executionTime: number | null;
}

export function SqlPanel({ sql, executionTime }: SqlPanelProps) {
  if (!sql) return null;

  return (
    <div className="sql-panel">
      <div className="sql-panel-header">
        <h3>Generated Secure SQL</h3>
        {executionTime !== null && (
          <div className="metrics-badge">
            <Clock size={14} />
            <span>{(executionTime / 1000).toFixed(2)}s execution</span>
          </div>
        )}
      </div>
      <code>{sql}</code>
    </div>
  );
}
