import { useId, type ReactNode } from 'react';

interface FieldRenderProps {
  id: string;
  describedById: string | undefined;
  invalid: boolean;
}

/* The control is a render prop so callers can spread register() onto it while this component wires
   the label, error and aria attributes. */
export function Field({
  label,
  error,
  children,
}: {
  label: ReactNode;
  error?: string;
  children: (props: FieldRenderProps) => ReactNode;
}) {
  const id = useId();
  const errorId = `${id}-error`;

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-medium text-text">
        {label}
      </label>
      {children({ id, describedById: error ? errorId : undefined, invalid: Boolean(error) })}
      {error && (
        <p id={errorId} className="text-xs text-danger">
          {error}
        </p>
      )}
    </div>
  );
}
