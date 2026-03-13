import { Activity, BarChart3, Clock, ThumbsUp, Zap } from "lucide-react";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  ResponsiveContainer,
} from "recharts";
import type { QueryHistoryItem } from "../types";

export function MetricsPage({ history }: { history: QueryHistoryItem[] }) {
  const totalQueries = history.length;
  const successfulQueries = history.filter((h) => h.isSuccessful).length;
  const successRate =
    totalQueries > 0
      ? ((successfulQueries / totalQueries) * 100).toFixed(1)
      : "0";

  const avgTime =
    totalQueries > 0
      ? (
          history.reduce((acc, curr) => acc + curr.executionTimeMs, 0) /
          totalQueries /
          1000
        ).toFixed(2)
      : "0";

  const positiveFeedback = history.filter(
    (h) => h.userFeedback === "up",
  ).length;

  const recentHistory = history.slice(0, 20).reverse();
  const chartData = recentHistory.map((h, i) => ({
    name: `Q${i + 1}`,
    time: (h.executionTimeMs / 1000).toFixed(2),
    success: h.isSuccessful,
    prompt: h.prompt.length > 30 ? h.prompt.substring(0, 30) + "..." : h.prompt,
  }));

  const CustomTooltip = ({ active, payload }: any) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div
          style={{
            backgroundColor: "#fff",
            padding: "10px",
            border: "1px solid #ccc",
            borderRadius: "4px",
          }}
        >
          <p
            style={{ margin: 0, fontWeight: "bold", color: "#333" }}
          >{`Query: ${data.name}`}</p>
          <p style={{ margin: 0, color: "#333" }}>{`Prompt: ${data.prompt}`}</p>
          <p style={{ margin: 0, color: data.success ? "#10b981" : "#ef4444" }}>
            {`Time: ${data.time} seconds`}
            {!data.success && " (Failed)"}
          </p>
        </div>
      );
    }

    return null;
  };

  return (
    <div className="page-container fade-in">
      <div className="page-header">
        <h1>
          <Activity className="page-icon" /> Metrics & Evaluations
        </h1>
        <p className="page-description">
          Performance analytics, success rates, and system evaluation telemetry.
        </p>
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
          <div className="telemetry-card" style={{ gridColumn: "span 2" }}>
            <h4>Execution History (Last 20 Queries)</h4>
            <div style={{ width: "100%", height: 300, marginTop: "20px" }}>
              <ResponsiveContainer>
                <AreaChart
                  data={chartData}
                  margin={{ top: 10, right: 30, left: 0, bottom: 0 }}
                >
                  <CartesianGrid
                    strokeDasharray="3 3"
                    vertical={false}
                    stroke="#E2E8F0"
                  />
                  <XAxis dataKey="name" stroke="#64748B" />
                  <YAxis
                    stroke="#64748B"
                    tickFormatter={(value) => `${value}s`}
                  />
                  <RechartsTooltip content={<CustomTooltip />} />
                  <Area
                    type="monotone"
                    dataKey="time"
                    stroke="#4F46E5"
                    fill="#EEF2FF"
                    strokeWidth={2}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="telemetry-card" style={{ gridColumn: "span 2" }}>
            <h4>Performance Stats</h4>
            <ul className="stats-list">
              <li>
                <span>Total Queries</span>
                <strong>{totalQueries}</strong>
              </li>
              <li>
                <span>Successful Queries</span>
                <strong style={{ color: "#10B981" }}>
                  {successfulQueries}
                </strong>
              </li>
              <li>
                <span>Total Failed Queries</span>
                <strong style={{ color: "#EF4444" }}>
                  {totalQueries - successfulQueries}
                </strong>
              </li>
              <li>
                <span style={{ paddingLeft: "1rem", color: "#64748B" }}>
                  ↳ Failed due to LLM
                </span>
                <strong style={{ color: "#EF4444" }}>
                  {
                    history.filter(
                      (h) =>
                        !h.isSuccessful &&
                        h.errorMessage?.includes("Eroare LLM"),
                    ).length
                  }
                </strong>
              </li>
              <li>
                <span style={{ paddingLeft: "1rem", color: "#64748B" }}>
                  ↳ Failed due to SQL Gen/Exec
                </span>
                <strong style={{ color: "#EF4444" }}>
                  {
                    history.filter(
                      (h) =>
                        !h.isSuccessful &&
                        h.errorMessage?.includes("Eroare SQL"),
                    ).length
                  }
                </strong>
              </li>
              <li>
                <span>Min Time</span>
                <strong>
                  {totalQueries > 0
                    ? (
                        Math.min(...history.map((h) => h.executionTimeMs)) /
                        1000
                      ).toFixed(2)
                    : 0}
                  s
                </strong>
              </li>
              <li>
                <span>Max Time</span>
                <strong>
                  {totalQueries > 0
                    ? (
                        Math.max(...history.map((h) => h.executionTimeMs)) /
                        1000
                      ).toFixed(2)
                    : 0}
                  s
                </strong>
              </li>
              <li>
                <span>Negative Feedback</span>
                <strong>
                  {history.filter((h) => h.userFeedback === "down").length}
                </strong>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
