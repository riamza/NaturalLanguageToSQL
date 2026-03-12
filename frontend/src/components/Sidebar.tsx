import { LayoutDashboard, Database, Activity, History as HistoryIcon } from 'lucide-react';

export type ViewState = 'query' | 'schema' | 'metrics' | 'history';

interface SidebarProps {
  currentView: ViewState;
  onViewChange: (view: ViewState) => void;
}

export function Sidebar({ currentView, onViewChange }: SidebarProps) {
  const navItems = [
    { id: 'query', label: 'Query Assistant', icon: LayoutDashboard },
    { id: 'schema', label: 'Schema Viewer', icon: Database },
    { id: 'metrics', label: 'Metrics & Evals', icon: Activity },
    { id: 'history', label: 'Query History', icon: HistoryIcon },
  ] as const;

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
            key={item.id}
            className={`nav-item ${currentView === item.id ? 'active' : ''}`}
            onClick={() => onViewChange(item.id as ViewState)}
          >
            <item.icon size={18} className="nav-icon" />
            <span className="nav-label">{item.label}</span>
          </button>
        ))}
      </nav>
      
      <div className="sidebar-footer">
        <div className="status-indicator">
          <span className="status-dot"></span>
          <span>System Online</span>
        </div>
      </div>
    </aside>
  );
}
