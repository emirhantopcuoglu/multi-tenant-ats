import { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import { useTranslation } from 'react-i18next';
import { Tabs, TabPanel, Textarea } from '@/components/ui';

interface MarkdownFieldProps {
  id: string;
  value: string;
  onChange: (value: string) => void;
  invalid?: boolean;
  describedById?: string;
  placeholder?: string;
}

/* Markdown description editor: a plain textarea for writing plus a rendered preview tab. We use
   react-markdown (well-maintained, sanitizes by default — no dangerouslySetInnerHTML) rather than
   hand-rolling a parser; the public job page renders the same markdown, so a preview here lets
   recruiters see the result before publishing. Styling is a few arbitrary variants instead of the
   typography plugin, which we don't otherwise need. */
export function MarkdownField({
  id,
  value,
  onChange,
  invalid,
  describedById,
  placeholder,
}: MarkdownFieldProps) {
  const { t } = useTranslation();
  const [tab, setTab] = useState('write');

  return (
    <Tabs
      value={tab}
      onValueChange={setTab}
      items={[
        { value: 'write', label: t('jobForm.write') },
        { value: 'preview', label: t('jobForm.preview') },
      ]}
    >
      <TabPanel value="write">
        <Textarea
          id={id}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          invalid={invalid}
          aria-describedby={describedById}
          placeholder={placeholder}
          rows={8}
        />
      </TabPanel>
      <TabPanel value="preview">
        {value.trim() ? (
          <div className="min-h-32 space-y-2 rounded-lg border border-border bg-bg px-3 py-2.5 text-sm leading-relaxed text-text [&_a]:text-accent [&_code]:rounded [&_code]:bg-divider [&_code]:px-1 [&_h1]:text-lg [&_h1]:font-semibold [&_h2]:text-base [&_h2]:font-semibold [&_ol]:list-decimal [&_ol]:pl-5 [&_ul]:list-disc [&_ul]:pl-5">
            <ReactMarkdown>{value}</ReactMarkdown>
          </div>
        ) : (
          <p className="min-h-32 rounded-lg border border-border bg-bg px-3 py-2.5 text-sm text-text-muted">
            {t('jobForm.previewEmpty')}
          </p>
        )}
      </TabPanel>
    </Tabs>
  );
}
