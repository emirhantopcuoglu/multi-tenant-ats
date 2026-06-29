import { useId, type ReactNode } from 'react';

interface FieldRenderProps {
  id: string;
  /** Pass to the control's aria-describedby; undefined when there's no error to point at. */
  describedById: string | undefined;
  invalid: boolean;
}

/* Labelled form field with accessible error wiring. The control is a render prop so the caller can
   spread react-hook-form's register() onto our Input/Select while we own the id ↔ label ↔ error
   relationships (htmlFor, aria-describedby, aria-invalid). */
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
