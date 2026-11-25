---
name: explainer
description: Builds an explainer, a page in the Workbench (`src/Workbench`) that shows the user something instead of a long answer in the chat - an architecture or a flow, options weighed side by side, a plan, a report or how something works - with the app's own bit BlazorUI components and theme, in light and dark, on every device and right to left, listed in the Workbench under Explainers and handed over as its address.
---

# Explainer: show it as a page in the Workbench

An explainer is a page an agent builds when an answer is easier to see than to read. It is written for the person who
asked, in their language, and it runs in the Workbench (the **workbench** skill, `.github/agents/workbench.agent.md`)
on the app's own components and theme: it looks like the product, follows light and dark and the accent the user tries,
reads on a phone, takes comments like any page there, and stays at one address while it changes.

Two examples ship with the Workbench: `src/Workbench/Components/Pages/Explainers/CiCd`, this project's CI/CD for the
options it was created with, and `src/Workbench/Components/Pages/Explainers/Payments`, a dated comparison of payment
providers and of the stores' rules. Read one before you build your first.

## 1. When to build one

- The answer is a picture or a comparison: how parts connect (an architecture, a flow, a pipeline), options weighed
  against each other (providers, libraries, approaches, prices), a plan with its steps and owners, a report of what was
  found or done, a walkthrough of how a part of the project works.
- Or the user will come back to it, show it to someone, or decide from it.
- A short answer, a single fact or a code change stays in the chat. When it is a close call, answer in the chat and offer
  the page in one line.
- It isn't a design of a page of the app, which the **workbench** skill designs, nor a long task's progress, which goes on
  the Progress page (its section 9). Under **ai-dlc**, a phase that shows the user a plan, a design's options or a
  report can show it as an explainer.
- Never put a secret, a key or a password on it, nor anything the user wouldn't show everyone who gets its link.

## 2. Plan it

- Settle one subject, who reads it and the one question it answers; that question, or a short name for it, is its title.
- Ground it in the subject. Read what it is about: the code, the configuration, `.github/workflows`, `.docs`, `specs/`.
  Read an outside fact, like a price, a fee or a rule, on its owner's own page, and date it. Use the real names, numbers,
  units and terms, never placeholder text, and never a figure you didn't read somewhere. An estimate says that it is one.
- Put the summary before the detail: a few lines at the top that answer the question, then the sections that back them.
- Let the structure say something true. Number only a real sequence, give each place or stage its own column, and give
  the options of a comparison the same rows in the same order, so they compare at a glance.
- Pick the components by what each part has to do, and start with `SearchBitBlazorUI` (`AGENTS.md` section 3): a
  sequence is a `BitTimeline`, options with the same facts are `BitCard`s with the same rows or a `BitDataGrid` when there
  are many, views of the same thing are a `BitPivot`, a filter is a `BitSearchBox` or a `BitChoiceGroup`, figures are a
  `BitChart` or bars drawn to scale, a what-if is a `BitSlider` or a `BitNumberField` with the result worked out on the
  page, a caveat is a `BitMessage` and a label is a `BitTag`. A diagram is a layout of its own, its boxes and arrows in the
  theme's tokens.

## 3. Build it

- One folder per explainer, `src/Workbench/Components/Pages/Explainers/<Name>/`: the page (`<Name>ExplainerPage.razor`,
  `.razor.cs` and `.razor.scss`), and a `Data` folder with its content as C# records when there is much of it, so the
  markup stays short and the facts sit in one place.
- Frame it with the blocks in `src/Workbench/Components/Explainers`: `ExplainerArticle` around it all, with the title,
  the eyebrow, the lead, where and when its facts were read, a note above the content and the language it is written in;
  an `ExplainerSection` per section, with an id a link can point at; and `ExplainerSources` at the end, with an
  `ExplainerSource` for each page or file a fact came from.
- Write it in the user's language, as plain text in the page: not through `Localizer` and not in `WorkbenchStrings`. It
  is a document for the person who asked, not a text of the app, so it isn't translated into the app's languages. Give
  `ExplainerArticle` that language: a right-to-left one then reads right to left whatever language the view is in.
- Follow `AGENTS.md` section 5 as for any page: bit components styled through their parameters, theme tokens in its
  `.razor.scss`, no color the theme doesn't have, and a color that stands for data in a `--data-color` property.
- Make it a polished document, not a wall of identical cards: a card or a border only where it sets a thing apart, one
  accent, repeated parts with the same edges and rows, running text about 65 characters wide, rows that stack at phone
  width and nothing that scrolls sideways but a wide table or diagram in a scroll box of its own. Draw bars and charts to
  one scale. Write the copy plainly, from the reader's side: name things the way they know them, use short sentences,
  and leave out the clever turns.
- It is complete at rest: everything to read is there once the page loads. It has nothing to load, so it doesn't go
  through the scenario state, and the data-state switches leave it as it is.
- What the reader changes on it, like a filter, a slider or a tab, lives in a bit Blex store, as on any Workbench page, so
  a reload and a screenshot keep it (the **workbench** skill, section 2). A new store is registered in
  `src/Workbench/Infrastructure/Extensions/IWorkbenchServiceCollectionExtensions.cs`, startup code that takes one restart.
  A page without such controls needs no store.
- Add it to `WorkbenchPageCatalog` under `CategoryExplainers`, with `HasAppShell = false`, as a document has no app shell
  around it, and the bit components it uses as its showcases. While you work, its title, description and caption are the
  plain text where the `nameof(WorkbenchStrings.…)` keys go, which the Workbench shows as written, so hot reload brings
  the page in without a restart.

## 4. Look at it

- Open it the way the **workbench** skill's section 4 says, at `/workbench/<key>?device=laptop-hidpi`, then on a phone,
  in dark, and right to left when it is written in such a language. `/api/screenshots/<key>` takes the same query keys,
  and `/api/checks/<key>` checks its accessibility and its colors.
- Text cut at one width, parts that overlap, a bar or a chart that isn't to scale and a color outside the theme are
  findings. Fix what you find in one pass rather than in a loop of looks.
- Read every fact once more against its source.

## 5. Hand it over

- Give the user its address, `/workbench/<key>?device=laptop-hidpi` on the Workbench's own address, where they read it and
  pin comments on it, and say in a sentence or two what it shows. Don't repeat the page in the chat.
- Listen to the review for it (`pages=<key>`) and answer its comments the way the **workbench** skill's sections 1 and 7
  say. A change keeps its key, so the address stays the same.
- To show it to someone on another machine, share it the way that skill's section 6 says.

## 6. Keep it or let it go

- When the user wants to keep it in the repository, move its catalog entry's texts into `WorkbenchStrings.resx` in every
  language the Workbench has, which its tests check, and commit it with the work it explains. Its content stays in the
  language it was written in.
- Otherwise delete its folder and its catalog entry once the user is done with it.
- What the user decides from it is a product decision: write it into the feature's spec (`AGENTS.md` section 6).

## Done when

- It answers its question at the top, and every fact on it comes from the project or from a source it names and dates.
- It reads well in light and dark, at phone and desktop widths, and right to left when it is written in such a language,
  with no finding of its own markup in `/api/checks/<key>`.
- The user has its address, and every comment on it is done or answered.
