import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tabs, TabPanel, Textarea } from '@/components/ui';
import { Markdown } from '@/components/Markdown';

interface MarkdownFieldProps {
  id: string;
  value: string;
  onChange: (value: string) => void;
  invalid?: boolean;
  describedById?: string;
  placeholder?: string;
}

/* Textarea plus a preview tab that renders like the public job page. */
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
          <Markdown className="min-h-32 rounded-lg border border-border bg-bg px-3 py-2.5">
            {value}
          </Markdown>
        ) : (
          <p className="min-h-32 rounded-lg border border-border bg-bg px-3 py-2.5 text-sm text-text-muted">
            {t('jobForm.previewEmpty')}
          </p>
        )}
      </TabPanel>
    </Tabs>
  );
}
