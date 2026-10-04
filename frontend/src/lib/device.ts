// A short name for a browser and system, from the User-Agent text the browser sent when it signed in. The list is for a person
// to tell their devices apart, so a guess that is close is enough. Order matters: Edge and Opera also say Chrome, and
// Chrome also says Safari, and an iPhone also says Mac.
const browsers: [RegExp, string][] = [
  [/Edg(e|A|iOS)?\//, 'Edge'],
  [/OPR\/|Opera/, 'Opera'],
  [/Firefox\/|FxiOS\//, 'Firefox'],
  [/Chrome\/|CriOS\//, 'Chrome'],
  [/Safari\//, 'Safari'],
]

const systems: [RegExp, string][] = [
  [/iPhone/, 'iPhone'],
  [/iPad/, 'iPad'],
  [/Android/, 'Android'],
  [/Windows/, 'Windows'],
  [/Mac OS X|Macintosh/, 'macOS'],
  [/CrOS/, 'ChromeOS'],
  [/Linux/, 'Linux'],
]

export function describeDevice(userAgent: string | null | undefined): string {
  if (!userAgent) {
    return 'Unknown device'
  }

  const browser = browsers.find(([pattern]) => pattern.test(userAgent))?.[1]
  const system = systems.find(([pattern]) => pattern.test(userAgent))?.[1]
  if (browser && system) {
    return `${browser} on ${system}`
  }
  return browser ?? system ?? 'Unknown device'
}
