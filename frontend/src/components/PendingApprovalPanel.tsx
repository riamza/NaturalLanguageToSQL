import { useEffect, useState } from "react";
import { AlertTriangle, Pencil, RotateCcw } from "lucide-react";

interface PendingApprovalPanelProps {
  pendingApproval: { sql: string; ir: any } | null;
  loading: boolean;
  onApprove: (editedSql?: string) => void;
  onCancel: () => void;
}

export function PendingApprovalPanel({
  pendingApproval,
  loading,
  onApprove,
  onCancel,
}: PendingApprovalPanelProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editedSql, setEditedSql] = useState("");

  // Resetează starea de editare ori de câte ori apare o nouă cerere de aprobare.
  useEffect(() => {
    setEditedSql(pendingApproval?.sql ?? "");
    setIsEditing(false);
  }, [pendingApproval?.sql]);

  if (!pendingApproval) return null;

  const isDirty = editedSql.trim() !== pendingApproval.sql.trim();

  return (
    <div className="sql-panel warning-border">
      <div className="sql-panel-header">
        <h3 className="warning-text">
          <AlertTriangle size={18} />
          Approval Required for Statement
        </h3>
        {!isEditing ? (
          <button
            type="button"
            className="btn-edit"
            onClick={() => setIsEditing(true)}
            disabled={loading}
            title="Edit the SQL before running it"
          >
            <Pencil size={14} />
            Edit
          </button>
        ) : (
          isDirty && (
            <button
              type="button"
              className="btn-edit"
              onClick={() => {
                setEditedSql(pendingApproval.sql);
                setIsEditing(false);
              }}
              disabled={loading}
              title="Revert to the generated SQL"
            >
              <RotateCcw size={14} />
              Reset
            </button>
          )
        )}
      </div>

      {isEditing ? (
        <textarea
          className="approval-sql-edit"
          value={editedSql}
          onChange={(e) => setEditedSql(e.target.value)}
          disabled={loading}
          spellCheck={false}
          rows={Math.min(Math.max(editedSql.split("\n").length, 3), 14)}
        />
      ) : (
        <code>{editedSql}</code>
      )}

      {isEditing && isDirty && (
        <p className="approval-edit-hint">
          Vei executa varianta editată manual (fără reparametrizare automată).
        </p>
      )}

      <div className="approval-actions">
        <button
          onClick={() => onApprove(isDirty ? editedSql : undefined)}
          disabled={loading || !editedSql.trim()}
          className="btn-approve"
        >
          {isDirty ? "Approve & Execute (edited)" : "Approve & Execute"}
        </button>
        <button onClick={onCancel} disabled={loading} className="btn-cancel">
          Cancel
        </button>
      </div>
    </div>
  );
}
