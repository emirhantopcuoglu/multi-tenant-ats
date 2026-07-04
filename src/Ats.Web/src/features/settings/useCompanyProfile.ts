import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getCompanyProfile, updateCompanyProfile } from './companyProfileApi';

export const companyProfileKey = ['company', 'profile'] as const;

export function useCompanyProfile() {
  return useQuery({
    queryKey: companyProfileKey,
    queryFn: getCompanyProfile,
  });
}

/* The PUT echoes the saved profile back, so write it straight into the cache instead of
   invalidating — no second round-trip for data we already hold. */
export function useUpdateCompanyProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateCompanyProfile,
    onSuccess: (profile) => queryClient.setQueryData(companyProfileKey, profile),
  });
}
