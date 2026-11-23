import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Spinner } from '../../components/ui/Spinner'
import { adminApi, formatDate } from '../../features/admin/adminApi'
import type { AdminLinkedPicker, AdminStats, AdminUser, LinkedStatusFilter, Paged, UserRoleFilter } from '../../features/admin/types'
import { ApiError } from '../../lib/api'

type View = UserRoleFilter | 'linked'
const VIEWS: View[] = ['all', 'picker', 'driver', 'orchard_owner', 'linked', 'admin', 'none']
const STATUSES: LinkedStatusFilter[] = ['all', 'pending', 'confirmed', 'declined', 'revoked']
const codeOf = (e: unknown) => (e instanceof ApiError ? (e.problem.code ?? 'unknown') : 'unknown')

/**
 * Admin panel: counts, then every account filtered by category (picker, driver, orchard owner, admin, none) or
 * the linked pickers without accounts. Filters live in the URL so Back and reload keep them.
 */
export function AdminPage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const view: View = VIEWS.find((v) => v === params.get('view')) ?? 'all'
  const status = (params.get('status') ?? 'all') as LinkedStatusFilter
  const search = params.get('q') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)
  const [draft, setDraft] = useState(search)
  const [stats, setStats] = useState<AdminStats | null>(null)

  useEffect(() => {
    adminApi.stats().then(setStats).catch(() => setStats(null))
  }, [])

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v === null || v === '' || (k === 'view' && v === 'all') || (k === 'status' && v === 'all')) next.delete(k)
      else next.set(k, v)
    }
    if (!('page' in changes)) next.delete('page')
    setParams(next)
  }

  const tiles: [string, number | undefined, string][] = [
    ['all', stats?.users, t('admin.stats.users')],
    ['picker', stats?.pickers, t('admin.stats.pickers')],
    ['driver', stats?.drivers, t('admin.stats.drivers')],
    ['orchard_owner', stats?.orchardOwners, t('admin.stats.orchard_owners')],
    ['linked', stats?.linkedPickers, t('admin.stats.linked', { confirmed: stats?.linkedPickersConfirmed ?? 0 })],
  ]

  return (
    <div className="mx-auto max-w-6xl space-y-6 px-4 py-10">
      <div>
        <h1 className="text-2xl font-semibold">{t('admin.title')}</h1>
        <p className="mt-1 text-stone-600">{t('admin.subtitle')}</p>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
        {tiles.map(([key, value, label]) => (
          <button
            key={key}
            type="button"
            onClick={() => update({ view: key, status: null })}
            className={`rounded-[var(--radius-card)] border bg-white p-4 text-left transition-colors hover:border-brand-600 ${view === key ? 'border-brand-600 ring-2 ring-brand-100' : 'border-stone-200'}`}
          >
            <span className="block text-2xl font-semibold text-brand-800">{value ?? '…'}</span>
            <span className="mt-1 block text-sm text-stone-600">{label}</span>
          </button>
        ))}
      </div>

      <Card>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex flex-wrap gap-2" role="tablist">
            {VIEWS.map((r) => (
              <button
                key={r}
                type="button"
                role="tab"
                aria-selected={view === r}
                onClick={() => update({ view: r, status: null })}
                className={`rounded-full px-3 py-1.5 text-sm font-medium ${view === r ? 'bg-brand-700 text-white' : 'bg-stone-100 text-stone-700 hover:bg-stone-200'}`}
              >
                {t(`admin.views.${r}`)}
              </button>
            ))}
          </div>
          <form
            className="flex gap-2"
            onSubmit={(e) => {
              e.preventDefault()
              update({ q: draft.trim() })
            }}
          >
            <input
              type="search"
              aria-label={t('admin.search')}
              placeholder={t('admin.search_placeholder')}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="h-10 w-full rounded-[var(--radius-control)] border border-stone-300 px-3 text-sm lg:w-72"
            />
            <Button type="submit" variant="secondary">
              {t('admin.search')}
            </Button>
          </form>
        </div>

        {view === 'linked' && (
          <div className="mt-4 flex flex-wrap gap-2">
            {STATUSES.map((s) => (
              <button
                key={s}
                type="button"
                onClick={() => update({ status: s })}
                className={`rounded-full border px-3 py-1 text-xs font-medium ${status === s ? 'border-brand-600 bg-brand-50 text-brand-800' : 'border-stone-200 text-stone-600'}`}
              >
                {s === 'all' ? t('admin.views.all') : t(`linked.status.${s}`)}
              </button>
            ))}
          </div>
        )}

        <div className="mt-5">
          {view === 'linked' ? (
            <LinkedTable status={status} search={search} page={page} onPage={(p) => update({ page: String(p) })} />
          ) : (
            <UsersTable role={view} search={search} page={page} onPage={(p) => update({ page: String(p) })} />
          )}
        </div>
      </Card>
    </div>
  )
}

/** Result of a list request, tagged with the filters it was loaded for, so stale results never show. */
interface Loaded<T> {
  key: string
  data?: T
  error?: string
}

function useLoaded<T>(key: string, state: Loaded<T> | null) {
  if (!state || state.key !== key) return { data: null, error: null }
  return { data: state.data ?? null, error: state.error ?? null }
}

function UsersTable({ role, search, page, onPage }: { role: UserRoleFilter; search: string; page: number; onPage: (p: number) => void }) {
  const { t, i18n } = useTranslation()
  const key = `${role}|${search}|${page}`
  const [state, setState] = useState<Loaded<Paged<AdminUser>> | null>(null)
  useEffect(() => {
    let cancelled = false
    adminApi
      .users(role, search, page)
      .then((data) => !cancelled && setState({ key, data }))
      .catch((e: unknown) => !cancelled && setState({ key, error: codeOf(e) }))
    return () => {
      cancelled = true
    }
  }, [key, role, search, page])
  const { data, error } = useLoaded(key, state)
  if (error) return <FormError code={error} />
  if (!data) return <Spinner />
  if (data.items.length === 0) return <p className="py-8 text-center text-stone-500">{t('admin.empty')}</p>

  return (
    <>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-stone-200 text-xs uppercase tracking-wide text-stone-500">
            <tr>
              <th className="py-2 pr-4 font-medium">{t('admin.col.name')}</th>
              <th className="py-2 pr-4 font-medium">{t('admin.col.contact')}</th>
              <th className="py-2 pr-4 font-medium">{t('admin.col.categories')}</th>
              <th className="py-2 pr-4 font-medium">{t('admin.col.linked')}</th>
              <th className="py-2 font-medium">{t('admin.col.registered')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-stone-100">
            {data.items.map((u) => (
              <tr key={u.id} className="hover:bg-stone-50">
                <td className="py-3 pr-4">
                  <Link to={`/admin/users/${u.id}`} className="font-medium text-brand-800 hover:underline">
                    {u.firstName ? `${u.firstName} ${u.lastName}` : t('admin.no_name')}
                  </Link>
                  {u.isAdmin && <Badge tone="dark">{t('admin.badge.admin')}</Badge>}
                  {!u.isActive && <Badge tone="red">{t('admin.badge.disabled')}</Badge>}
                </td>
                <td className="py-3 pr-4">
                  <span className="block break-all">{u.email}</span>
                  <span className="block text-stone-500">
                    {u.phoneNumber ?? '—'} · {u.emailConfirmed ? t('dashboard.verified') : t('dashboard.not_verified')}
                  </span>
                </td>
                <td className="py-3 pr-4">
                  {u.picker && <Badge>{t('categories.picker')}</Badge>}
                  {u.driver && <Badge>{t('categories.driver')}</Badge>}
                  {u.orchardOwner && <Badge>{t('categories.orchard')}</Badge>}
                  {!u.picker && !u.driver && !u.orchardOwner && <span className="text-stone-400">—</span>}
                </td>
                <td className="py-3 pr-4">{u.linkedPickers || '—'}</td>
                <td className="py-3 text-stone-600">{formatDate(u.createdAt, i18n.language)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager data={data} onPage={onPage} />
    </>
  )
}

function LinkedTable({ status, search, page, onPage }: { status: LinkedStatusFilter; search: string; page: number; onPage: (p: number) => void }) {
  const { t, i18n } = useTranslation()
  const key = `${status}|${search}|${page}`
  const [state, setState] = useState<Loaded<Paged<AdminLinkedPicker>> | null>(null)
  useEffect(() => {
    let cancelled = false
    adminApi
      .linkedPickers(status, search, page)
      .then((data) => !cancelled && setState({ key, data }))
      .catch((e: unknown) => !cancelled && setState({ key, error: codeOf(e) }))
    return () => {
      cancelled = true
    }
  }, [key, status, search, page])
  const { data, error } = useLoaded(key, state)
  if (error) return <FormError code={error} />
  if (!data) return <Spinner />
  if (data.items.length === 0) return <p className="py-8 text-center text-stone-500">{t('admin.empty')}</p>

  return (
    <>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-stone-200 text-xs uppercase tracking-wide text-stone-500">
            <tr>
              <th className="py-2 pr-4 font-medium">{t('admin.col.name')}</th>
              <th className="py-2 pr-4 font-medium">{t('admin.col.contact')}</th>
              <th className="py-2 pr-4 font-medium">{t('admin.col.status')}</th>
              <th className="py-2 pr-4 font-medium">{t('admin.col.linked_by')}</th>
              <th className="py-2 font-medium">{t('admin.col.payment')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-stone-100">
            {data.items.map((l: AdminLinkedPicker) => (
              <tr key={l.id}>
                <td className="py-3 pr-4">
                  <span className="block font-medium">{l.firstName} {l.lastName}</span>
                  <span className="block text-stone-500">{t('personal.personal_id')} •••• {l.personalIdNumberLast4}</span>
                </td>
                <td className="py-3 pr-4">
                  <span className="block">{l.phoneNumber}</span>
                  <span className="block text-stone-500">{l.actualAddress}</span>
                </td>
                <td className="py-3 pr-4">
                  <Badge tone={l.consentStatus === 'confirmed' ? 'green' : l.consentStatus === 'pending' ? 'amber' : 'stone'}>
                    {t(`linked.status.${l.consentStatus}`)}
                  </Badge>
                  {l.consentedAt && <span className="block text-stone-500">{formatDate(l.consentedAt, i18n.language)}</span>}
                </td>
                <td className="py-3 pr-4">
                  <Link to={`/admin/users/${l.representativeId}`} className="font-medium text-brand-800 hover:underline">
                    {l.representativeFirstName ? `${l.representativeFirstName} ${l.representativeLastName}` : l.representativeEmail}
                  </Link>
                  <span className="block break-all text-stone-500">{l.representativeEmail}</span>
                </td>
                <td className="py-3">{l.payToRepresentative ? t('admin.pay_rep') : t('admin.pay_own')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager data={data} onPage={onPage} />
    </>
  )
}

function Pager({ data, onPage }: { data: Paged<unknown>; onPage: (p: number) => void }) {
  const { t } = useTranslation()
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize))
  return (
    <div className="mt-4 flex items-center justify-between text-sm text-stone-600">
      <span>{t('admin.total', { count: data.total })}</span>
      {pages > 1 && (
        <div className="flex items-center gap-2">
          <Button variant="secondary" disabled={data.page <= 1} onClick={() => onPage(data.page - 1)}>←</Button>
          <span>{t('admin.page', { page: data.page, pages })}</span>
          <Button variant="secondary" disabled={data.page >= pages} onClick={() => onPage(data.page + 1)}>→</Button>
        </div>
      )}
    </div>
  )
}

const tones = {
  green: 'bg-brand-50 text-brand-800',
  amber: 'bg-accent-100 text-accent-600',
  red: 'bg-red-50 text-red-700',
  stone: 'bg-stone-100 text-stone-700',
  dark: 'bg-stone-800 text-white',
}

export function Badge({ children, tone = 'green' }: { children: React.ReactNode; tone?: keyof typeof tones }) {
  return <span className={`mr-1 mt-1 inline-block rounded-full px-2 py-0.5 text-xs font-medium ${tones[tone]}`}>{children}</span>
}
