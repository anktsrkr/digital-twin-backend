-- ==============================================================================
-- 03_rls_policies.sql
-- Conversational Digital Twin Resume Assistant
-- Row-Level Security (RLS) Policies
-- ==============================================================================

-- Mock auth.uid() function if running in standalone PostgreSQL outside Supabase
create or replace function auth.uid()
returns uuid
language sql stable
as $$
    select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid;
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

-- 6. Trigger to automatically create a recruiter_profile on auth.users sign-up
create or replace function public.handle_new_recruiter()
returns trigger
language plpgsql
security definer set search_path = public
as $$
declare
    user_email text;
    email_domain text;
    inferred_co text;
begin
    user_email := new.email;
    email_domain := split_part(user_email, '@', 2);
    
    -- Infer company name from domain if not generic (e.g. google.com -> Google)
    if email_domain not in ('gmail.com', 'outlook.com', 'yahoo.com', 'hotmail.com', 'icloud.com', 'proton.me', 'protonmail.com') then
        inferred_co := initcap(split_part(email_domain, '.', 1));
    else
        inferred_co := null;
    end if;

    insert into public.recruiter_profiles (id, email, domain, company_inferred, first_login_at, last_active_at)
    values (new.id, user_email, email_domain, inferred_co, now(), now())
    on conflict (id) do update 
    set last_active_at = now();

    return new;
end;
$$;

-- Attach trigger if auth.users table exists
do $$ begin
    if exists (select 1 from information_schema.tables where table_schema = 'auth' and table_name = 'users') then
        drop trigger if exists on_auth_user_created on auth.users;
        create trigger on_auth_user_created
            after insert on auth.users
            for each row execute function public.handle_new_recruiter();
    end if;
end $$;
