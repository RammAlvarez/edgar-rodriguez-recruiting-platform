import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatTooltip } from '@angular/material/tooltip';
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import { prompt } from '../openings/prompt-dialog';

export interface NoteDraft {
  readonly html: string;
  readonly json: string;
}

type Control = 'h1' | 'h2' | 'h3' | 'bold' | 'italic' | 'ordered' | 'bullet' | 'link';

/**
 * The Notas editor (SPEC §4.2, PLAN D74): exactly H1, H2, H3, Bold, Italic, numbered list, bulleted list and hyperlink.
 * Every other StarterKit node and mark is off, so a paste cannot bring them in; the server sanitizes again.
 */
@Component({
  selector: 'app-note-editor',
  imports: [MatButton, MatIconButton, MatIcon, MatTooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toolbar" role="toolbar" aria-label="Formato">
      @for (c of controls; track c.id) {
        <button matIconButton type="button" [attr.aria-label]="c.label" [matTooltip]="c.label" [attr.aria-pressed]="active()[c.id]"
                [class.on]="active()[c.id]" (click)="apply(c.id)">
          @if (c.text) {
            <span class="heading">{{ c.text }}</span>
          } @else {
            <mat-icon fontSet="material-symbols-outlined">{{ c.icon }}</mat-icon>
          }
        </button>
      }
    </div>
    <div #host class="editor" [attr.aria-label]="'Nota'"></div>
    <div class="actions">
      <button matButton="filled" type="button" [disabled]="empty() || busy()" (click)="submit()">
        {{ editing() ? 'Actualizar nota' : 'Guardar nota' }}
      </button>
      <button matButton type="button" (click)="clear()">Cancelar</button>
    </div>
  `,
  styles: `
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: 2px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-bottom: 0;
      border-radius: 8px 8px 0 0;
      padding: 2px;
    }
    .toolbar .on {
      background: var(--mat-sys-secondary-container);
    }
    .heading {
      font: var(--mat-sys-label-large);
      font-weight: 700;
    }
    .editor {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 0 0 8px 8px;
      padding: 8px 12px;
      min-height: 6lh;
      cursor: text;
    }
    .editor ::ng-deep .ProseMirror {
      outline: none;
      min-height: 5lh;
    }
    .editor ::ng-deep .ProseMirror :first-child {
      margin-top: 0;
    }
    .actions {
      display: flex;
      gap: 8px;
      margin-top: 8px;
    }
  `,
})
export class NoteEditor {
  readonly busy = input(false);
  readonly saved = output<NoteDraft>();
  readonly cancelled = output<void>();

  private readonly dialog = inject(MatDialog);
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private editor: Editor | null = null;

  readonly empty = signal(true);
  readonly editing = signal(false);
  readonly active = signal<Record<Control, boolean>>({ h1: false, h2: false, h3: false, bold: false, italic: false, ordered: false, bullet: false, link: false });

  readonly controls: readonly { id: Control; label: string; text?: string; icon?: string }[] = [
    { id: 'h1', label: 'Título 1', text: 'H1' },
    { id: 'h2', label: 'Título 2', text: 'H2' },
    { id: 'h3', label: 'Título 3', text: 'H3' },
    { id: 'bold', label: 'Negritas', icon: 'format_bold' },
    { id: 'italic', label: 'Cursiva', icon: 'format_italic' },
    { id: 'ordered', label: 'Lista numerada', icon: 'format_list_numbered' },
    { id: 'bullet', label: 'Lista con viñetas', icon: 'format_list_bulleted' },
    { id: 'link', label: 'Hipervínculo', icon: 'link' },
  ];

  constructor() {
    afterNextRender(() => {
      this.editor = new Editor({
        element: this.host().nativeElement,
        extensions: [
          StarterKit.configure({
            heading: { levels: [1, 2, 3] },
            blockquote: false,
            code: false,
            codeBlock: false,
            hardBreak: false,
            horizontalRule: false,
            strike: false,
            underline: false,
            link: {
              openOnClick: false,
              autolink: true,
              defaultProtocol: 'https',
              protocols: ['http', 'https', 'mailto'],
              HTMLAttributes: { target: '_blank', rel: 'noopener' },
            },
          }),
        ],
        onTransaction: () => this.refresh(),
      });
      this.refresh();
    });
    inject(DestroyRef).onDestroy(() => this.editor?.destroy());
  }

  /** Loads a saved note back into the editor («Editar»); the button reads «Actualizar nota». */
  load(contentJson: string | null, contentHtml: string): void {
    if (!this.editor) return;
    this.editor.commands.setContent(contentJson ? (JSON.parse(contentJson) as object) : contentHtml);
    this.editing.set(true);
    this.editor.commands.focus('end');
  }

  clear(): void {
    this.editor?.commands.clearContent();
    this.editing.set(false);
    this.cancelled.emit();
  }

  /** Called by the parent after a successful save. */
  reset(): void {
    this.editor?.commands.clearContent();
    this.editing.set(false);
  }

  submit(): void {
    if (!this.editor || this.editor.isEmpty) return;
    this.saved.emit({ html: this.editor.getHTML(), json: JSON.stringify(this.editor.getJSON()) });
  }

  async apply(control: Control): Promise<void> {
    const e = this.editor;
    if (!e) return;
    const chain = e.chain().focus();
    switch (control) {
      case 'h1':
      case 'h2':
      case 'h3':
        chain.toggleHeading({ level: Number(control[1]) as 1 | 2 | 3 }).run();
        return;
      case 'bold':
        chain.toggleBold().run();
        return;
      case 'italic':
        chain.toggleItalic().run();
        return;
      case 'ordered':
        chain.toggleOrderedList().run();
        return;
      case 'bullet':
        chain.toggleBulletList().run();
        return;
      case 'link': {
        if (e.isActive('link')) {
          chain.unsetLink().run();
          return;
        }
        const url = await prompt(this.dialog, { title: 'Agregar hipervínculo', label: 'URL', confirm: 'Aplicar', kind: 'text' });
        if (url) e.chain().focus().extendMarkRange('link').setLink({ href: /^(https?:|mailto:)/i.test(url) ? url : `https://${url}` }).run();
        return;
      }
    }
  }

  private refresh(): void {
    const e = this.editor;
    if (!e) return;
    this.empty.set(e.isEmpty);
    this.active.set({
      h1: e.isActive('heading', { level: 1 }),
      h2: e.isActive('heading', { level: 2 }),
      h3: e.isActive('heading', { level: 3 }),
      bold: e.isActive('bold'),
      italic: e.isActive('italic'),
      ordered: e.isActive('orderedList'),
      bullet: e.isActive('bulletList'),
      link: e.isActive('link'),
    });
  }
}
