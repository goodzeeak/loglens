# Report an inaccurate diagnosis

Select the incident and click **Prepare diagnostic feedback**. Describe what LogLens said, why you think it is wrong and the outcome you expected. Do not add passwords, license keys, raw dumps or private account details.

Continue to the redacted preview. It includes the version, category, record identities, selected normalized fields, rule IDs, findings and recommendations. Raw XML, binary WHEA payloads, DNS names, arbitrary event fields and investigation notes are omitted. This minimizes exposure but may prevent complete reproduction of a binary/undocumented record.

Review the entire preview, save the HTML, then review it again before sharing. Redaction is best effort. Nothing is sent automatically, and LogLens does not create a GitHub issue. If you choose, open [an inaccurate-diagnosis issue](https://github.com/goodzeeak/loglens/issues/new?template=inaccurate-diagnosis.yml) and provide only reviewed information. You can report the rule IDs and expected behavior without attaching any file.

Maintainers must reproduce a misleading result with a synthetic fixture, correct the responsible module or investigation rule, and run the full accuracy suite. A passing build does not override a failing fixture.
