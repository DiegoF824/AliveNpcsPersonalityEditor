# AliveNpcs Personality Manager — documentação técnica e revisão

## Escopo e versões verificadas

O `AliveNpcsPersonalityEditor` é um mod SMAPI que fornece uma interface dentro do Stardew Valley para consultar e editar os perfis usados pelo AliveNpcs. A versão revisada nesta sessão é a `2.1.1` e declara dependência obrigatória de `Lucas.AliveNpcs` `1.4.5` ou superior.

Na primeira etapa da revisão, os dois repositórios foram sincronizados com `git pull --ff-only`:

- Personality Editor: branch `master`, já estava atualizado no commit `60ce072`.
- AliveNpcs experimental: branch `main`, atualizado por fast-forward de `8fa26a1` para `d99d103`; o `manifest.json` passou a indicar a versão `1.4.7`.

Nenhum arquivo-fonte do AliveNpcs foi alterado nesta sessão.

### Sincronização posterior do fork

Depois da revisão inicial, o fork `DiegoF824/AliveNpcsPersonalityEditor` foi sincronizado novamente com seu upstream. Essa operação substituiu à força o histórico remoto: `origin/master` saiu do commit `e143d33` e passou para `887a144`.

As duas linhas não possuíam um ancestral comum utilizável:

- o fork sincronizado continha a versão `1.1.0`, com a interface original de edição de personalidades;
- o trabalho local continha a versão `2.1.1`, com editor do fazendeiro, presets, catálogo, dados estruturados, desativação individual de NPCs e as correções descritas neste documento.

Antes de alinhar a branch local, o estado `e143d33` foi preservado de três formas:

- branch local `backup/pre-fork-sync-e143d33`;
- patch completo entre `887a144` e `e143d33`;
- bundle Git com o histórico anterior.

Em seguida, `master` foi alinhada a `origin/master` no commit `887a144` e toda a árvore funcional da versão `2.1.1` foi reaplicada sobre essa base. Dessa forma, o histórico sincronizado do fork foi mantido e as funcionalidades desenvolvidas localmente ficaram prontas para serem versionadas em um novo commit.

A alteração mais recente do fork, relativa ao editor de texto multilinha, também foi comparada. A implementação da versão `2.1.1` já contém o comportamento equivalente e ainda inclui:

- rolagem de linhas;
- navegação vertical com as setas;
- posicionamento do cursor por clique;
- navegação para início e fim da linha visual;
- correção que impede o Backspace de apagar dois caracteres.

## Como o mod funciona

### Inicialização

`ModEntry` é o ponto de entrada SMAPI. Durante a inicialização, ele:

1. lê `config.json`;
2. cria os armazenamentos de personalidades, presets e dados locais do fazendeiro;
3. registra os eventos de entrada, abertura de menu e inicialização do jogo;
4. obtém a API pública do AliveNpcs pelo Unique ID `Lucas.AliveNpcs`;
5. registra o diretório local de overrides no AliveNpcs;
6. configura a integração opcional com Generic Mod Config Menu (GMCM);
7. prepara o catálogo remoto, quando habilitado.

As teclas configuráveis abrem a interface principal ou diretamente a aba do fazendeiro. A interface possui áreas para o perfil do fazendeiro, perfis de NPCs e catálogo de presets.

### Edição de NPCs

A lista de NPCs editáveis vem da API do AliveNpcs. Para cada NPC, o editor consulta a personalidade padrão, o estado de override, dados básicos e, quando necessário, retrato e nome exibido.

As alterações de personalidade são gravadas em arquivos JSON individuais no diretório `overrides`. Depois da gravação ou remoção de um override, o editor solicita ao AliveNpcs que recarregue as personalidades customizadas.

O checkbox de desativação por NPC usa diretamente `IsNpcDisabled` e `SetNpcDisabled`. Portanto, o editor apenas apresenta e altera o estado mantido pelo próprio AliveNpcs; a decisão de impedir diálogos, cartas, fofocas e arcos narrativos continua sendo aplicada pelo AliveNpcs.

### Perfil do fazendeiro

O editor lê e atualiza a ficha do personagem por meio da API do AliveNpcs. Existe armazenamento local como apoio para a interface, mas a ficha ativa é controlada pelo AliveNpcs.

Campos que alteram dados-base do personagem ficam condicionados à opção correspondente de configuração. O editor também informa ao AliveNpcs se esses dados devem ser incluídos na geração de prompts.

### Presets e catálogo

Presets locais ficam no diretório `presets`. Quando o catálogo remoto está habilitado, `GalleryService` usa HTTP para verificar a saúde do serviço, listar presets e realizar as operações suportadas pelo servidor configurado.

O endereço do catálogo pode ser alterado pelo GMCM. O mesmo cliente HTTP é reaproveitado quando possível e descartado quando o recurso é desabilitado.

### Persistência

- `config.json`: preferências gerais e teclas.
- `overrides/<npc>.json`: personalidade customizada por NPC.
- `presets/*.json`: presets locais.
- `custom_personalities.json`: formato legado; ao ser encontrado, é migrado para arquivos individuais e preservado como backup `.bak`.
- ficha de personagem: lida e atualizada principalmente pela API do AliveNpcs.

## APIs usadas

### API pública do AliveNpcs

O editor mantém uma interface local contendo apenas o subconjunto necessário da API de `Lucas.AliveNpcs`. Todos os métodos abaixo foram conferidos contra a interface pública da versão `1.4.7`.

| Método | Uso no editor |
| --- | --- |
| `GetDefaultPersonality` | Obtém o perfil padrão de um NPC. |
| `GetVanillaNpcNames` | Obtém a lista de NPCs do jogo-base. |
| `GetSveNpcNames` | Obtém a lista de NPCs reconhecidos do Stardew Valley Expanded. |
| `GetEditableNpcNames` | Obtém a lista consolidada de NPCs que podem ser editados. |
| `IsNpcDisabled` | Consulta se as interações AliveNpcs estão desativadas para um NPC. |
| `SetNpcDisabled` | Ativa ou desativa as interações AliveNpcs para um NPC. |
| `HasCustomPersonality` | Verifica se há override ativo para o NPC. |
| `RegisterCustomPersonalityDirectory` | Registra o diretório de overrides do editor. |
| `ReloadCustomPersonalities` | Solicita a releitura dos overrides. |
| `ReloadCharacterSheet` | Recarrega a ficha ativa do personagem. |
| `UpdateCharacterSheet` | Atualiza campos da ficha do personagem. |
| `GetCharacterSheet` | Lê a ficha atual do personagem. |
| `GetBaseCharacterData` | Lê dados-base usados pelos campos avançados. |
| `GetBaseDisplayName` | Obtém o nome-base exibido para um personagem. |
| `SetCharacterDataPromptEnabled` | Controla o uso de dados-base nos prompts do AliveNpcs. |

O contrato completo do AliveNpcs está documentado também em `AliveNpcsRevamp-experimental/API.md`.

### Generic Mod Config Menu

Quando `spacechase0.GenericModConfigMenu` está instalado, o editor registra opções para teclas, catálogo e campos avançados. Essa integração é opcional; a ausência do GMCM não impede o funcionamento do editor.

### API exposta pelo Personality Editor

`PersonalityEditorApi` permite que outros mods abram:

- o menu principal do editor;
- o menu já posicionado na aba do fazendeiro.

## Alterações e portes realizados

### Correções

- A migração de `custom_personalities.json` agora chama a persistência completa imediatamente. Isso cria os arquivos por NPC e o backup do legado antes que uma edição posterior possa deixar parte dos dados sem migrar.
- O comando de reset do GMCM agora cria a configuração padrão. Antes, ele apenas relia a configuração já salva e não restaurava efetivamente os padrões.
- O serviço do catálogo agora implementa `IDisposable`, reaproveita o cliente HTTP ao trocar apenas a URL e o descarta ao desabilitar o catálogo.
- Foram adicionadas três traduções ausentes em alemão, francês e italiano: bloqueio dos dados de personagem, título do aviso e texto do aviso de alteração desses dados.

### Remoção de duplicações e redundâncias

- As opções JSON idênticas usadas pelos armazenamentos de personalidade e presets foram centralizadas em `EditorJson`.
- Desenho de bordas e scrollbars passou a usar diretamente os helpers compartilhados de `EditorTheme`.
- Wrappers de botões que apenas encaminhavam os mesmos parâmetros foram removidos.
- Parâmetros de `GalleryService` recebidos e descartados por painéis que não o utilizavam foram removidos.

Essas mudanças preservam as funcionalidades, os formatos JSON atuais e a aparência da interface.

## Validação

- `dotnet build` do Personality Editor em `Release`, com analisadores .NET no nível mais recente e deploy desabilitado: **sucesso, 0 avisos e 0 erros**.
- Todos os arquivos de tradução foram validados como JSON.
- Todas as traduções possuem o mesmo conjunto de chaves do idioma padrão.
- O subconjunto da API declarado pelo editor foi comparado com a API pública atual do AliveNpcs: nenhum método necessário está ausente.
- `git diff --check`: nenhuma inconsistência de whitespace.
- Testes do AliveNpcs: **96 de 97 passaram**. O único teste que falhou, sem qualquer alteração local no AliveNpcs, foi `VietnameseLanguageAssetTests.CoreVietnameseCatalogMatchesDefaultKeysAndMarkers`, porque o catálogo vietnamita não contém a chave `hub.tabBeachAlbum` presente no idioma padrão. Esse problema pertence ao estado sincronizado do repositório AliveNpcs e não ao Personality Editor.

### Validação após o porte para o fork sincronizado

- `master` e `origin/master` foram confirmadas em `887a144` antes da aplicação das alterações.
- O porte completo foi aplicado sem conflitos.
- `git diff --check` não encontrou inconsistências de whitespace.
- Todos os arquivos de tradução foram novamente validados como JSON e mantêm o mesmo conjunto de chaves do idioma padrão.
- Compilação `Release` com analisadores .NET no nível mais recente: **0 avisos e 0 erros**.
- Deploy confirmado em `Stardew Valley/Mods/AliveNpcsPersonalityEditor`.
- O hash SHA-256 da DLL implantada é idêntico ao da DLL produzida pela compilação.
- A versão implantada continua sendo `2.1.1`.
- O teste manual dentro do jogo foi concluído sem regressões observadas.
