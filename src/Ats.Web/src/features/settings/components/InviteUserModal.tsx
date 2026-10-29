import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Field, Input, Modal, Select, useToast } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { ROLES, type Role } from '@/types/enums';
import { useInviteUser } from '../useInviteUser';

interface InviteUserModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

// A basic shape check; the backend decides the rest.
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const DEFAULT_ROLE: Role = 'Recruiter';

interface FormErrors {
  email?: string;
  role?: string;
}

/* The backend checks the role and duplicate emails; its codes are mapped to messages here. */
export function InviteUserModal({ open, onOpenChange }: InviteUserModalProps) {
  const { t } = useTranslation();
  const { toast } = useToast();
  const invite = useInviteUser();

  const [email, setEmail] = useState('');
  const [role, setRole] = useState<Role>(DEFAULT_ROLE);
  const [errors, setErrors] = useState<FormErrors>({});

  // Reset when the dialog opens. Done during render, not in an effect, so the old draft is never painted.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setEmail('');
      setRole(DEFAULT_ROLE);
      setErrors({});
    }
  }

  const handleSubmit = () => {
    const trimmedEmail = email.trim();

    const nextErrors: FormErrors = {};
    if (!trimmedEmail) nextErrors.email = t('settings.invite.emailRequired');
    else if (!EMAIL_PATTERN.test(trimmedEmail)) nextErrors.email = t('settings.invite.emailInvalid');

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    invite.mutate(
      { email: trimmedEmail, role },
      {
        onSuccess: () => {
          onOpenChange(false);
          toast({ title: t('settings.invite.sent'), tone: 'success' });
        },
        onError: (error) => {
          const { code } = toApiError(error);
          if (code === 'invite.email_in_use') {
            setErrors({ email: t('settings.invite.emailInUse') });
            return;
          }
          toast({ title: t('settings.invite.error'), tone: 'danger' });
        },
      },
    );
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('settings.invite.title')}
      description={t('settings.invite.subtitle')}
      className="max-w-md"
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={invite.isPending}>
            {t('common.cancel')}
          </Button>
          <Button onClick={handleSubmit} disabled={invite.isPending}>
            {t('settings.invite.send')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label={t('settings.invite.email')} error={errors.email}>
          {({ id, describedById, invalid }) => (
            <Input
              id={id}
              type="email"
              aria-describedby={describedById}
              invalid={invalid}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder={t('settings.invite.emailPlaceholder')}
            />
          )}
        </Field>

        <Field label={t('settings.invite.role')}>
          {({ id }) => (
            <Select id={id} value={role} onChange={(event) => setRole(event.target.value as Role)}>
              {ROLES.map((value) => (
                <option key={value} value={value}>
                  {t(`role.${value}`)}
                </option>
              ))}
            </Select>
          )}
        </Field>
      </div>
    </Modal>
  );
}
