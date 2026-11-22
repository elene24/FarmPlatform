export function Spinner() {
  return (
    <div className="flex justify-center py-20" role="status" aria-live="polite">
      <span className="size-8 animate-spin rounded-full border-4 border-brand-200 border-t-brand-700" />
    </div>
  )
}
