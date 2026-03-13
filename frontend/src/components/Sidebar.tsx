import {
  LayoutDashboard,
  Database,
  Activity,
  History as HistoryIcon,
  MessageSquare
} from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";
import type { QueryHistoryItem } from "../types";

interface SidebarProps {
  history?: QueryHistoryItem[];
  onHistoryClick?: (prompt: string) => void;
}

export function Sidebar({ history = [], onHistoryClick }: SidebarProps) {
  const location = useLocation();
  const navigate = useNavigate();

  const navItems = [
    { path: "/", label: "Query Assistant", icon: LayoutDashboard },
    { path: "/schema", label: "Schema Viewer", icon: Database },
    { path: "/metrics", label: "Metrics & Evals", icon: Activity },
    { path: "/history", label: "Query History", icon: HistoryIcon },
  ] as const;

  const recentHistory = history.slice(0, 5);

  return (
    <aside className="sidebar premium-sidebar">
      <div className="sidebar-header">
        <h2 className="premium-title">
          <span className="text-gradient">NL to SQL</span>
          <br />
          <span className="subtitle">Premium Edition</span>
        </h2>
      </div>
      <nav className="sidebar-nav">
        {navItems.map((item) => (
          <button
            key={item.path}
            className={`nav-item ${location.pathname === item.path ? "active" : ""}`}
            onClick={() => navigate(item.path)}
          >
            <item.icon size={18} className="nav-icon" />
            <span className="nav-label">{item.label}</span>
          </button>
        ))}
      </nav>

      <div className="sidebar-footer">
        {recentHistory.length > 0 && (
          <div className="recent-history-sidebar">
            <h3 className="recent-history-title">Recent Queries</h3>
            <div className="recent-history-list">
              {recentHistory.map((item) => (
                <button
                  key={item.id}
                  className="recent-history-item"
                  onClick={() => {
                    if (onHistoryClick) {
                      onHistoryClick(item.prompt);
                    }
                  }}
                  title={item.prompt}
                >
                  <MessageSquare size={14} className="recent-history-icon" />
                  <span className="recent-history-text">{item.prompt}</span>
                </button>
              ))}
            </div>
          </div>
        )}
        <div className="status-indicator">
          <span className="status-dot"></span>
          <span>System Online</span>
        </div>
      </div>
    </aside>
  );
}
