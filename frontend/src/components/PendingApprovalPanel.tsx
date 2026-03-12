import { AlertTriangle } from "lucide-react";

interface PendingApprovalPanelProps {
  pendingApproval: { sql: string; ir: any } | null;
  loading: boolean;
  onApprove: () => void;
  onCancel: () => void;
}

export function PendingApprovalPanel({
  pendingApproval,
  loading,
  onApprove,
  onCancel,
}: PendingApprovalPanelProps) {
  if (!pendingApproval) return null;

  return (
    <div className="sql-panel warning-border">
      <div className="sql-panel-header">
        <h3 className="warning-text">
          <AlertTriangle size={18} />
          Approval Required for Statement
        </h3>
      </div>
      <code>{pendingApproval.sql}</code>
      <div className="approval-actions">
        <button onClick={onApprove} disabled={loading} className="btn-approve">
          Approve & Execute
        </button>
        <button onClick={onCancel} disabled={loading} className="btn-cancel">
          Cancel
        </button>
      </div>
    </div>
  );
}
