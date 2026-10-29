import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getCompanyProfile, updateCompanyProfile } from './companyProfileApi';

export const companyProfileKey = ['company', 'profile'] as const;

export function useCompanyProfile() {
  return useQuery({
    queryKey: companyProfileKey,
    queryFn: getCompanyProfile,
  });
}

/* The PUT returns the saved profile, so update the cache directly. */
export function useUpdateCompanyProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateCompanyProfile,
    onSuccess: (profile) => queryClient.setQueryData(companyProfileKey, profile),
  });
}
