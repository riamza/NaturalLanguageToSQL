import { Activity, BarChart3, Clock, ThumbsUp, Zap } from "lucide-react";
import type { QueryHistoryItem } from "../types";

export function MetricsPage({ history }: { history: QueryHistoryItem[] }) {
  const totalQueries = history.length;
  const successfulQueries = history.filter(h => h.isSuccessful).length;
  const successRate = totalQueries > 0 ? (successfulQueries / totalQueries * 100).toFixed(1) : "0";
  
  const avgTime = totalQueries > 0 
    ? (history.reduce((acc, curr) => acc + curr.executionTimeMs, 0) / totalQueries / 1000).toFixed(2)
    : "0";

  const positiveFeedback = history.filter(h => h.userFeedback === "up").length;

  return (
    <div className="page-container fade-in">
      <div className="page-header">
        <h1>
          <Activity className="page-icon" /> Metrics & Evaluations
        </h1>
        <p className="page-description">Performance analytics, success rates, and system evaluation telemetry.</p>
      </div>

      <div className="metrics-grid">
        <div className="metric-card">
          <div className="metric-icon-wrapper blue">
            <BarChart3 size={24} />
          </div>
          <div className="metric-content">
            <span className="metric-value">{totalQueries}</span>
            <span className="metric-label">Total Queries</span>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-icon-wrapper green">
            <Zap size={24} />
          </div>
          <div className="metric-content">
            <span className="metric-value">{successRate}%</span>
            <span className="metric-label">Success Rate</span>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-icon-wrapper purple">
            <Clock size={24} />
          </div>
          <div className="metric-content">
            <span className="metric-value">{avgTime}s</span>
            <span className="metric-label">Avg Execution Time</span>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-icon-wrapper orange">
            <ThumbsUp size={24} />
          </div>
          <div className="metric-content">
            <span className="metric-value">{positiveFeedback}</span>
            <span className="metric-label">Positive Feedback</span>
          </div>
        </div>
      </div>
      
      <div className="metrics-charts-placeholder">
        <h3>System Telemetry</h3>
        <div className="telemetry-grid">
          <div className="telemetry-card">
            <h4>Execution History</h4>
            <div className="bar-chart">
              {history.slice(-10).map((h, i) => {
                const height = Math.min(100, Math.max(10, (h.executionTimeMs / 5000) * 100));
                return (
                  <div 
                    key={i} 
                    className={`bar ${h.isSuccessful ? 'success' : 'error'}`}
                    style={{ height: `${height}%` }}
                    title={`${(h.executionTimeMs / 1000).toFixed(2)}s`}
                  ></div>
                );
              })}
            </div>
          </div>
          
          <div className="telemetry-card">
            <h4>Performance Stats</h4>
            <ul className="stats-list">
              <li>
                <span>Min Time</span>
                <strong>{totalQueries > 0 ? (Math.min(...history.map(h => h.executionTimeMs)) / 1000).toFixed(2) : 0}s</strong>
              </li>
              <li>
                <span>Max Time</span>
                <strong>{totalQueries > 0 ? (Math.max(...history.map(h => h.executionTimeMs)) / 1000).toFixed(2) : 0}s</strong>
              </li>
              <li>
                <span>Failed Queries</span>
                <strong>{totalQueries - successfulQueries}</strong>
              </li>
              <li>
                <span>Negative Feedback</span>
                <strong>{history.filter(h => h.userFeedback === "down").length}</strong>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
