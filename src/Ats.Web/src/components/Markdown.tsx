import ReactMarkdown from 'react-markdown';
import { cn } from '@/lib/cn';

/* react-markdown sanitizes by default. Shared by the job form preview and the public job page so
   both render the same. */
const ELEMENT_CLASSES =
  'space-y-2 text-sm leading-relaxed text-text [&_a]:text-accent [&_code]:rounded [&_code]:bg-divider [&_code]:px-1 [&_h1]:text-lg [&_h1]:font-semibold [&_h2]:text-base [&_h2]:font-semibold [&_ol]:list-decimal [&_ol]:pl-5 [&_ul]:list-disc [&_ul]:pl-5';

export function Markdown({ children, className }: { children: string; className?: string }) {
  return (
    <div className={cn(ELEMENT_CLASSES, className)}>
      <ReactMarkdown>{children}</ReactMarkdown>
    </div>
  );
}
