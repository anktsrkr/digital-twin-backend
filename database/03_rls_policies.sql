-- ==============================================================================
-- 03_rls_policies.sql
-- Conversational Digital Twin Resume Assistant
-- Row-Level Security (RLS) Policies
-- ==============================================================================

-- Mock auth.uid() function to extract Logto / JWT sub claim
create or replace function auth.uid()
returns text
language sql stable
as $$
    select nullif(current_setting('request.jwt.claim.sub', true), '');
$$;

-- 1. Enable RLS on all tables
alter table public.resume_chunks enable row level security;
alter table public.recruiter_profiles enable row level security;
alter table public.recruiter_conversations enable row level security;
alter table public.disposable_email_domains enable row level security;

-- 2. Resume Chunks: Public read access (recruiter chats need to retrieve context)
create policy "Allow authenticated users and service role to read resume chunks"
on public.resume_chunks for select
using (true);

-- 3. Recruiter Profiles:
-- Recruiter can view and update their own profile
create policy "Users can view own recruiter profile"
on public.recruiter_profiles for select
using (auth.uid() = id or auth.uid() is null);

create policy "Users can insert own recruiter profile"
on public.recruiter_profiles for insert
with check (auth.uid() = id or auth.uid() is null);

create policy "Users can update own recruiter profile"
on public.recruiter_profiles for update
using (auth.uid() = id or auth.uid() is null);

-- 4. Recruiter Conversations:
-- Recruiter can view their own conversations; Service role / Backend inserts queries
create policy "Users can view own conversations"
on public.recruiter_conversations for select
using (auth.uid() = recruiter_id or auth.uid() is null);

create policy "Users can insert own conversations"
on public.recruiter_conversations for insert
with check (auth.uid() = recruiter_id or auth.uid() is null);

-- 5. Disposable Email Domains: Read-only access
create policy "Allow read on disposable email domains"
on public.disposable_email_domains for select
using (true);

