import { describe, expect, it } from 'vitest'
import { describeDevice } from '../device'

const agents = {
  chromeMac: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36',
  safariMac: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15',
  safariIphone: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1',
  chromeIphone: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/141.0.0.0 Mobile/15E148 Safari/604.1',
  firefoxWindows: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0',
  edgeWindows: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0',
  chromeAndroid: 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Mobile Safari/537.36',
  operaLinux: 'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 OPR/120.0.0.0',
}

describe('the device label', () => {
  it.each([
    [agents.chromeMac, 'Chrome on macOS'],
    [agents.safariMac, 'Safari on macOS'],
    [agents.safariIphone, 'Safari on iPhone'],
    [agents.chromeIphone, 'Chrome on iPhone'],
    [agents.firefoxWindows, 'Firefox on Windows'],
    [agents.edgeWindows, 'Edge on Windows'],
    [agents.chromeAndroid, 'Chrome on Android'],
    [agents.operaLinux, 'Opera on Linux'],
  ])('reads %#', (agent, label) => {
    expect(describeDevice(agent)).toBe(label)
  })

  it.each([[null], [undefined], ['']])('says unknown for %j', (agent) => {
    expect(describeDevice(agent)).toBe('Unknown device')
  })

  it('gives what it can find when only one part is known', () => {
    expect(describeDevice('curl/8.4.0 (Linux)')).toBe('Linux')
    expect(describeDevice('SomeBrowser Firefox/1.0')).toBe('Firefox')
    expect(describeDevice('something else')).toBe('Unknown device')
  })
})
