import nextDynamic from 'next/dynamic';

const UploadForm = nextDynamic(() => import('./UploadForm'), { ssr: true });

export default function UploadPage() {
  return <UploadForm />;
}
