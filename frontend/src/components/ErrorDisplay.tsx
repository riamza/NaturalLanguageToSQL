import { Sparkles } from "lucide-react";

interface ErrorDisplayProps {
  error: string;
  prompt: string;
  loading: boolean;
  onRetry: (retryPrompt: string) => void;
}

export function ErrorDisplay({
  error,
  prompt,
  loading,
  onRetry,
}: ErrorDisplayProps) {
  if (!error) return null;

  const separator = "\n\n**Sugestie AI:**";
  if (error.includes(separator)) {
    const [dbError, aiSuggestion] = error.split(separator);
    const cleanError = dbError.replace("Eroare SQL: ", "").trim();
    const cleanSuggestion = aiSuggestion.trim();

    return (
      <div className="error-panel">
        <div className="error-sys">
          <strong>❗ Eroare Sistem:</strong> {cleanError}
        </div>
        <div className="error-ai">
          <div className="error-ai-text">
            <strong>💡 Sugestie AI:</strong> {cleanSuggestion}
          </div>
          <button
            type="button"
            className="btn-retry"
            disabled={loading}
            onClick={() =>
              onRetry(
                `Cerința inițială: "${prompt}".\n\nExecuția a eșuat cu eroarea: "${cleanError}".\n\nTe rog refă query-ul ținând cont de această sugestie: "${cleanSuggestion}"`,
              )
            }
          >
            <Sparkles size={16} />
            Reface automat folosind sugestia
          </button>
        </div>
      </div>
    );
  }

  return <div className="error-message default-error">{error}</div>;
}
