# Weather App — Framework de Automação Mobile

Um framework de automação com Appium + C#/NUnit para o app Android Weather App
(`apps/weather-app.apk`), cobrindo os quatro fluxos que o desafio menciona
explicitamente: **Cadastro, Login, Busca de Clima, Logout** — além de uma execução
agendada via BrowserStack, pra rodar em dispositivo real sem precisar manter um
emulador local ligado o tempo todo.

> Este é o README de apoio, em português. O documento principal, avaliado (em inglês,
> conforme pedido no briefing), é o [`README.md`](README.md).

## Tecnologias

- C#
- .NET 8
- NUnit 3
- Appium 2 (driver UiAutomator2) — emulador local ou BrowserStack App Automate
- Page Object Model + Component Objects
- Serilog (logs em console + arquivo, com rotação)
- Allure 2 (relatórios HTML, com histórico de tendência entre execuções)
- Microsoft.Extensions.Configuration (config em camadas: JSON + variáveis de ambiente)
- GitHub Actions (publicação do relatório no GitHub Pages; execução agendada via BrowserStack)

## Arquitetura

O framework é propositalmente pequeno e enxuto — quatro camadas, cada uma com uma
responsabilidade, sem nenhuma camada que existe só pra "parecer" um padrão de projeto:

```
Tests  →  Page Objects  →  BasePage (Click / Type / GetText / IsAbsent)  →  AndroidDriver
                ↑                        ↓
          Components              WaitHelper (único lugar onde as esperas são configuradas)
```

### Os testes expressam comportamento, nada mais

```csharp
[Test]
public void Login_WithValidCredentials_NavigatesToLandingScreen()
{
    var loginPage = RegisterFreshUser(out var user);

    var landingPage = loginPage.LoginWith(user.Email, user.Password);

    Assert.That(landingPage.IsDisplayedOnScreen(), Is.True,
        "Expected the landing screen (Get Started CTA) after a valid login.");
}
```

Sem locators, sem `FindElement`, sem espera, sem configuração de driver. Um teste só
responde "o que está sendo validado" — o resto é responsabilidade de outra camada.

### Page Objects concentram locators e ações de negócio de uma tela

```csharp
public sealed class LoginPage : BasePage
{
    private static readonly By EmailInput =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.EditText\").instance(0)");

    private static readonly By LoginButton =
        MobileBy.AndroidUIAutomator("new UiSelector().className(\"android.widget.Button\").text(\"Login\")");

    public LandingPage LoginWith(string email, string password)
    {
        EnterEmail(email);
        EnterPassword(password);
        return SubmitLogin();
    }
    // ...
}
```

A `LoginPage` sabe o que significa "logar" nesse app. Ela não reimplementa como
esperar, clicar ou digitar — isso vem herdado da `BasePage`.

### Components representam uma região reutilizável da UI, não uma tela inteira

```csharp
public sealed class LocationResultList : BasePage
{
    private static By ResultRowAt(int index) =>
        MobileBy.AndroidUIAutomator(
            "new UiSelector().className(\"androidx.recyclerview.widget.RecyclerView\")" +
            $".childSelector(new UiSelector().clickable(true).instance({index}))");

    public ForecastPage SelectResultAt(int index) { Click(ResultRowAt(index)); return new ForecastPage(Driver, Wait); }

    public bool HasNoResults() => IsAbsent(SuggestionsList);
}
```

A lista de sugestões do autocomplete tem seu próprio ciclo de vida (presente vs.
ausente), distinto da responsabilidade da `SearchPage` (que só cuida do campo de
busca) — juntar as duas faria o teste negativo (que valida ausência) parecer estranho
dentro de uma classe cujos outros métodos assumem presença.

### BasePage é a única classe que toca Selenium/Appium diretamente

```csharp
public abstract class BasePage
{
    protected readonly WebDriver Driver;
    protected readonly WaitHelper Wait;

    protected void Click(By locator) => Wait.WaitForClickable(locator).Click();

    protected void Type(By locator, string text)
    {
        var element = Wait.WaitForVisible(locator);
        element.Clear();
        element.SendKeys(text);
    }
}
```

Todo Page Object e Component herda dessa classe em vez de compor um objeto de "ações"
separado — uma única superfície de interação pra todo o framework, e exatamente um
lugar (`WaitHelper`) onde as esperas explícitas são configuradas. Não existe nenhuma
chamada a `Thread.Sleep` em nenhum lugar do código.

### DriverFactory monta a sessão só a partir da configuração

```csharp
public static AndroidDriver CreateAndroidDriver()
{
    var settings = ConfigurationProvider.Settings.Appium;
    return settings.UseBrowserStack ? CreateBrowserStackDriver(settings) : CreateLocalDriver(settings);
}
```

Dispositivo, caminho do APK, timeouts e até o *alvo* da execução (emulador local vs.
BrowserStack) são configuração, nunca uma mudança de código. Veja em
[Decisões](#decisões) por que a BrowserStack existe como uma segunda opção aqui em vez
de rodar só localmente pra sempre.

### TestBase controla o único ciclo de vida que todas as fixtures compartilham

```csharp
[SetUp]
public void BaseSetUp()
{
    Driver = DriverFactory.CreateAndroidDriver();
    Wait = new WaitHelper(Driver, ConfigurationProvider.Settings.Timeouts);
}

[TearDown]
public void BaseTearDown()
{
    if (failed) ScreenshotHelper.Capture(Driver, context.Test.Name);
    AllureReportWriter.StopAndWriteTestCase();
    Driver?.Quit();
}
```

Um driver por **teste**, não por fixture ou por sessão — o app persiste estado no
dispositivo, então compartilhar um driver entre testes vazaria estado de sessão de um
teste pro outro (isso foi um bug real, confirmado durante o desenvolvimento — veja
Decisões). O trade-off é velocidade (~15-20s de overhead de reset do app por teste); a
alternativa era instabilidade.

### Estrutura de pastas

```
mobile-coding-challenge/
├── apps/
│   └── weather-app.apk
├── .github/workflows/
│   ├── smoke-test.yml                   # a cada push/PR: 4 testes centrais na BrowserStack, publica no Pages
│   └── scheduled-browserstack-run.yml   # 1x ao dia: suíte completa de 32 testes na BrowserStack, publica no Pages
├── docs/                                # relatório Allure gerado, versionado pro GitHub Pages (snapshot inicial)
├── src/WeatherApp.MobileTests/
│   ├── Config/       # AppiumSettings, TimeoutSettings, TestUserSettings, ConfigurationProvider
│   ├── Drivers/      # DriverFactory (local + BrowserStack), AppiumServerManager
│   ├── Core/         # BasePage, WaitHelper
│   ├── Pages/        # LoginPage, RegisterPage, LandingPage, SearchPage, ForecastPage, SettingsPage
│   ├── Components/   # LocationResultList
│   ├── Support/      # LoggerConfigurator, ScreenshotHelper, TestUserFactory, AllureReportWriter
│   ├── Tests/        # TestBase + RegistrationTests, LoginTests, WeatherSearchTests, LogoutTests
│   ├── appsettings.json                 # versionado, sem segredos
│   ├── appsettings.local.json.example   # copiar pra appsettings.local.json (ignorado no git) pra overrides locais
│   └── allureConfig.json
├── WeatherApp.MobileTests.sln
├── README.md
└── README.pt-BR.md
```

### O que eu propositalmente não coloquei

Cada um desses seria uma escolha legítima em outra escala, e cada um seria
over-engineering aqui:

- **Uma camada de `Repository`/container de DI** — quatro telas e um punhado de testes
  não precisam de indireção sobre a própria indireção.
- **Uma interface por Page Object** (`ILoginPage`) — nada troca de implementação; uma
  interface aqui existiria só pra dizer "eu usei interface".
- **XPath como estratégia principal de locator** — `AndroidUIAutomator`/`UiSelector` é
  o mecanismo nativo, mais rápido e recomendado pelo próprio Android; XPath seria um
  fallback que nunca precisei usar.
- **Uma classe `Actions` separada por Page** (`LoginPageActions` ao lado de
  `LoginPage`) — essa separação só compensa quando a superfície de ações de uma Page é
  enorme; as nossas não são.
- **Retry automático na suíte inteira** — mascara instabilidade real em vez de
  expô-la. Um retry *pontual* e visível está listado em Melhorias Futuras, não
  implementado agora.
- **Um `AllLocators.cs` global** — os locators ficam como constantes privadas ao lado
  da Page/Component dona deles, não em um arquivo gigante e sem contexto.

## Como Executar

### Pré-requisitos

- **.NET 8 SDK**
- **Node.js** (pro Appium) e **Java 17+** (pras ferramentas Android — o `avdmanager`
  especificamente precisa de 17+; o servidor Appium em si roda bem com 11+)
- **Android SDK** com `platform-tools` (`adb`), `emulator`, e pelo menos um AVD (este
  projeto foi construído/testado num AVD **Pixel 8 / API 35**)
- **Appium 2.x** e o driver **UiAutomator2**:
  ```bash
  npm install -g appium@2
  appium driver install uiautomator2@4.2.9
  ```
  (Fixe a versão do driver — as versões mais novas do `uiautomator2` visam o Appium 3,
  não o 2; a 4.2.9 é a última que suporta os dois explicitamente.)
- **Allure commandline**:
  ```bash
  brew install allure        # macOS
  # ou: scoop install allure # Windows
  # ou baixe de https://github.com/allure-framework/allure2/releases
  ```

### 1. Iniciar o emulador

```bash
emulator -avd Pixel_8_API_35
```

Espere o boot completo (`adb devices` deve mostrar `device`, não `offline`).

**Se o seu AVD tiver outro nome** (liste os seus com `emulator -list-avds`), não
precisa recriar um pra ficar igual — copie `appsettings.local.json.example` pra
`appsettings.local.json` e defina `Appium:DeviceName` com o nome do seu:
```json
{ "Appium": { "DeviceName": "Nome_Do_Seu_AVD" } }
```
Esse arquivo é ignorado pelo git, então é exatamente o lugar pensado pra esse tipo de
override por máquina (veja Configuração abaixo).

### 2. Restaurar, compilar, testar

```bash
dotnet restore
dotnet build
dotnet test
```

Por padrão o framework **sobe seu próprio servidor Appium local** (veja
`Appium:ManageServerLifecycle` em `appsettings.json`). Se o Appium já estiver rodando,
ou você estiver apontando pra um grid remoto, defina `"ManageServerLifecycle": false`
no `appsettings.local.json`.

Uma execução local completa leva alguns minutos: cada teste reseta o app
(`noReset:false`) pra garantir que todos comecem sempre na mesma tela de Login,
independente do que o teste anterior deixou — veja [Decisões](#decisões).

### 3. Ver os relatórios

```bash
cd src/WeatherApp.MobileTests/bin/Debug/net8.0
allure generate allure-results --clean -o allure-report
allure open allure-report
```

Os logs ficam em `logs/test-run-<data>.log`, screenshots de falha em `Screenshots/` —
ambos ao lado dos binários de teste, e ambos anexados automaticamente aos testes que
falharem no relatório Allure também.

### 4. Rodar contra a BrowserStack em vez do emulador (opcional)

Copie `appsettings.local.json.example` pra `appsettings.local.json` e configure:
```json
{
  "Appium": {
    "UseBrowserStack": true,
    "BrowserStackUsername": "...",
    "BrowserStackAccessKey": "...",
    "BrowserStackAppUrl": "bs://... (obtido ao subir o APK - veja abaixo)"
  }
}
```
Suba o APK uma vez pra obter o `app_url`:
```bash
curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
  -X POST "https://api-cloud.browserstack.com/app-automate/upload" \
  -F "file=@apps/weather-app.apk"
```
Depois rode `dotnet test` normalmente — o `DriverFactory` já entra no branch da
BrowserStack automaticamente, sem precisar mudar código. É exatamente isso que o
`.github/workflows/scheduled-browserstack-run.yml` automatiza no CI (subindo o APK a
cada execução, usando secrets do repositório em vez de um arquivo local).

### Configuração

Todas as configurações ficam em `appsettings.json` (versionado, sem segredos) e podem
ser sobrescritas via `appsettings.local.json` (ignorado no git) ou variáveis de
ambiente com prefixo `WEATHERAPP_` (ex.: `WEATHERAPP_TestUser__Password`, ou
`WEATHERAPP_Appium__UseBrowserStack` no CI). Nada — nome do dispositivo, timeouts,
caminho do APK, senha da conta de teste, credenciais da BrowserStack — está hard-coded
em uma classe.

## Escopo da Automação (perspectiva de QA)

O briefing lista Cadastro, Login, Busca de Clima e Logout como fluxos de exemplo pra um
app genérico — este app implementa exatamente esses quatro. A suíte cobre cada um com
4 casos positivos e 4 negativos (32 testes, em 4 fixtures). Cada caso abaixo —
incluindo quais regras de validação existem e qual texto de erro elas produzem — foi
confirmado manualmente contra o app real (`adb`/`uiautomator`) *antes* de virar teste;
nada aqui foi adivinhado só olhando a UI.

| Fixture | Casos positivos | Casos negativos |
|---|---|---|
| `RegistrationTests` | usuário novo válido · login imediato após cadastro · senha no tamanho mínimo válido (6 caracteres, valor de fronteira) · caracteres especiais no nome completo | campos vazios · senha/confirmação não coincidem · e-mail duplicado · senha abaixo do mínimo (fronteira) |
| `LoginTests` | credenciais válidas · e-mail case-insensitive · re-login após logout · identidade correta exibida em Settings | senha errada · e-mail inexistente · campos vazios · só o e-mail preenchido |
| `WeatherSearchTests` | cidade válida · busca em minúsculas · selecionar uma sugestão que não é a primeira · duas buscas sequenciais independentes | busca sem sentido · busca vazia · busca só com espaços · busca numérica |
| `LogoutTests` | volta pra Login · re-login depois · cadastrar outra conta depois · funciona após navegação profunda (busca) | senha errada continua rejeitada · e-mail inexistente continua rejeitado · conta continua existindo (recadastrá-la falha) · sessão não sobrevive a reinício do app |

**Por que 32 e não os "4 testes bem justificados" que o próprio briefing sugere?** A
frase do briefing — "o objetivo não é maximizar o número de testes" — está correta, e
eu levo isso a sério: cada um desses 32 é um comportamento real, distinto e confirmado
do app, não enchimento pra bater um número. Os quatro casos "positivos" de cada fluxo
são variações de fronteira/robustez (tamanho mínimo de senha, e-mail
case-insensitive, seleção de sugestão que não é a primeira), não quatro cópias do
mesmo caminho feliz; os quatro "negativos" são quatro entradas diferentes sendo
rejeitadas, não uma única asserção repetida. Se a filosofia do briefing pesar mais que
a contagem específica, o escopo mínimo defensável e honesto é **um positivo + um
negativo por fluxo** (8 testes) — o primeiro caso listado de cada coluna, em cada
fixture, é esse mínimo; o resto é adicional, não obrigatório.

Algumas decisões de escopo que vale destacar:

- **Cadastro não tem teste negativo de "formato de e-mail inválido"** porque o app não
  tem essa validação — um endereço malformado (`not-an-email`) é aceito. Confirmado na
  mão; testar um erro que nunca aparece seria um teste permanentemente quebrado, não
  um teste útil.
- **Os casos negativos de Busca de Clima validam ausência, não uma mensagem de erro** —
  uma busca sem sentido, vazia, só com espaços ou numérica produzem o mesmo
  comportamento real: a lista de sugestões simplesmente nunca aparece. Não existe
  string de erro pra validar.
- **Os casos "negativos" de Logout são checagens de regressão/fronteira, não validação
  de entrada.** Logout não recebe nenhuma entrada do usuário, então, diferente dos
  outros três fluxos, não há nada de inválido pra submeter. Esses quatro confirmam que
  o logout não deixa o app num estado sutilmente quebrado: a autenticação continua
  rejeitando credenciais ruins depois, os dados da conta continuam intactos (é uma
  ação de sessão, não uma limpeza de dados), e a sessão não persiste silenciosamente
  após reiniciar o app (verificado encerrando e reabrindo o app de verdade, via
  Appium, no meio do teste — não só navegando "voltar" dentro do app).

Cada teste cria seu próprio usuário descartável via `TestUserFactory` (e-mail único
por teste), então todo teste é independente e repetível, sem dado compartilhado entre
fixtures e sem precisar resetar o banco local do app entre execuções.

## Decisões

### Por que não usei AccessibilityId — e o que usei no lugar

O roteiro genérico desse tipo de README assume que os elementos são localizados por
accessibility id. Eu conferi antes de escrever qualquer código: extraindo a hierarquia
da UI (`adb shell uiautomator dump`) em todas as telas — Login, Cadastro, Busca,
Configurações — ficou claro que **nenhum elemento interativo tem `resource-id` ou
`content-desc`**. Só containers de framework (`nav_host`, `action_bar_root`) têm id;
todo botão, campo e label que um teste realmente toca não tem nenhum dos dois.
`AccessibilityId` simplesmente não é uma opção aqui — usá-lo significaria que o
framework não funciona contra o app real.

Também descobri, preenchendo os formulários de Login/Cadastro manualmente antes de
escrever código, que os `EditText` só reportam o texto de hint como `text` **enquanto
estão vazios** — no instante em que você digita algo, `text` vira o que foi digitado.
Isso descarta localizar campos pelo label visível também, já que o locator pararia de
funcionar exatamente no momento em que o teste faz a coisa pra qual ele existe.

O que usei no lugar, por tipo de elemento:
- **Botões e links** — texto estático via `UiSelector().text(...)`. Seguro, porque o
  texto de um botão não muda enquanto o usuário interage com ele.
- **Campos de texto** — `className("android.widget.EditText").instance(n)`, ou seja,
  posição entre os campos daquela tela. Frágil em abstrato, estável na prática pra um
  formulário fixo e pequeno — a única opção que sobrevive ao app sendo realmente usado.
- **Lista de sugestões do autocomplete** — linhas localizadas via
  `childSelector(new UiSelector().clickable(true).instance(n))`, então nunca fixa um
  nome de cidade e pode selecionar qualquer linha por posição.
- **Mensagens de validação/status** (Login, Cadastro) — as duas telas renderizam
  qualquer que seja a mensagem aplicável no *mesmo* slot condicional (confirmado via
  dump: sempre o 3º `TextView`), então um único locator `instance(2)` por tela cobre
  todos os casos de validação, em vez de um locator por mensagem.

Num projeto real, meu primeiro passo seria conseguir que o time mobile configurasse
`AutomationProperties.AutomationId` no XAML do app (é um app .NET MAUI, então é uma
mudança pequena e idiomática pro time deles) — isso permitiria mover toda a estratégia
de locators de campo pra `AccessibilityId` e eliminar de vez a fragilidade de
"localizar por posição".

### Por que um driver por teste, não por fixture

O app persiste o estado de login no dispositivo. Tentei compartilhar estado de forma
mais agressiva primeiro (`noReset:true`, mais rápido — evita o custo de ~15-20s de
reset do app por teste) e encontrei um bug real e reproduzível: um teste que deixa o
usuário logado (todos, exceto o de Logout) faz a *próxima* sessão de teste abrir
direto na tela pós-login, pulando a tela de Login — quebrando a suposição de todo Page
Object sobre onde ele começa. `noReset:false` custa velocidade, mas garante
determinismo, que importa mais aqui.

### Por que os resultados do Allure são escritos manualmente, sem `[AllureNUnit]`

O atributo de ação `[AllureNUnit]` do pacote `Allure.NUnit` também gerencia um
conceito de "container de teste" pra agrupar setup/teardown de fixture. Nessa
combinação exata — NUnit 3.14, `NUnit3TestAdapter` 4.5, .NET 8, `Allure.NUnit` 2.15.0 —
essa pilha de containers é encerrada duas vezes (uma por fixture, outra pela suíte
implícita externa do assembly), derrubando todo o processo de teste com
`InvalidOperationException: No container context is active` **depois** que todo teste
já tinha terminado e passado/falhado corretamente. O `Support/AllureReportWriter.cs`
chama `StartTestCase`/`StopTestCase`/`WriteTestCase` do `AllureLifecycle` diretamente e
nunca abre um container, produzindo o mesmo JSON de `allure-results` sem passar pelo
caminho de código que quebra — isolado na própria classe, então a única integração que
já quebrou uma vez fica num lugar único e substituível.

### Por que o CI usa a BrowserStack em vez do emulador local, em duas camadas

Rodar uma suíte no CI num emulador *local* significaria manter algum emulador Android +
servidor Appium ligados — custo real, e runners hospedados pelo GitHub não conseguem
fazer isso de forma confiável de qualquer jeito (veja Limitações). Apontar o CI pra
BrowserStack App Automate faz a execução não custar nada pra hospedar: o
`DriverFactory` ramifica em `Appium:UseBrowserStack` e monta um conjunto de
capabilities `bstack:options` em vez de um de emulador local; o `AppiumServerManager`
nem tenta subir um servidor local nesse modo. O APK é enviado pra BrowserStack do zero
a cada execução via API REST deles, em vez de ficar versionado em algum lugar, e as
únicas credenciais envolvidas — `BROWSERSTACK_USERNAME`/`BROWSERSTACK_ACCESS_KEY` — são
secrets do GitHub Actions, nunca commitadas. O `TestBase` também reporta o resultado
real do NUnit (passou/falhou) de volta pra BrowserStack via a API
`browserstack_executor` deles (`Drivers/BrowserStackReporter.cs`) — sem essa chamada, a
BrowserStack só sabe que a sessão do Appium não travou, não se as asserções realmente
passaram, então toda sessão apareceria sem marcação no dashboard deles,
independentemente do resultado real.

Dois workflows dividem essa mesma abordagem em duas cadências, pela mesma razão que
CI/CD geralmente tem uma camada rápida e uma lenta:

- **`smoke-test.yml`** — roda a cada push/PR pra `main`. Filtra pra 4 testes, um
  cenário positivo central por fluxo (Cadastro, Login, Busca, Logout) — de propósito,
  os mesmos 4 que eram o escopo mínimo original deste projeto antes de crescer pra 32.
  Rápido, e leve em minutos de BrowserStack, então pode rodar a cada mudança.
- **`scheduled-browserstack-run.yml`** — roda 1x ao dia (`cron: "0 6 * * *"`, mais
  `workflow_dispatch` pra uma execução sob demanda quando quiser) contra a **suíte
  completa de 32 testes**. Planos trial da BrowserStack têm uma cota mensal de minutos
  limitada, e rodar os 32 a cada push esgotaria essa cota em poucos dias — depois
  disso, o CI passaria a falhar por cota, não por uma regressão real, um sinal pior do
  que uma cadência mais lenta pro conjunto completo.

Os dois workflows recuperam `allure-results/history` a partir do que estiver publicado
no momento no GitHub Pages (via `curl`, best-effort, dos arquivos `history/*.json` do
relatório anterior) antes de gerar, e compartilham o mesmo `concurrency: group: pages`,
então ficam na fila em vez de atropelar o deploy um do outro. O Allure só constrói o
gráfico de tendência a partir dessa pasta de histórico, então sem esse passo toda
execução publicaria um relatório sem histórico nenhum — com ele, o histórico se acumula
entre execuções (das duas camadas, no mesmo gráfico) sem nunca commitar o relatório em
si no git.

**Configuração única:**
```bash
gh secret set BROWSERSTACK_USERNAME
gh secret set BROWSERSTACK_ACCESS_KEY
```
Depois, disparar qualquer um dos dois manualmente quando quiser:
```bash
gh workflow run smoke-test.yml
gh workflow run scheduled-browserstack-run.yml
```

## O que Não Foi Implementado / Limitações

- **A suíte no emulador local especificamente não roda no CI.** Rodar um emulador
  Android de forma confiável em runners hospedados pelo GitHub (KVM/virtualização
  aninhada, tempo de boot, instabilidade) é trabalho real e separado. O caminho via
  BrowserStack é o substituto compatível com CI, e esse sim roda a cada push/PR
  (`smoke-test.yml`) — só que com um subconjunto de 4 testes, não os 32 completos
  (isso fica pra camada diária).
- **Perfil único de dispositivo.** Tudo é validado num único AVD (Pixel 8 / API 35)
  localmente, e uma única configuração (Google Pixel 8 / Android 14) na BrowserStack.
  Sem device farm, sem matriz de tamanhos de tela/versões de SO.
- **Locators de campo são por posição, não por id** — uma correção de verdade exige
  mudança no próprio app (adicionar `AutomationId`), fora do controle deste
  repositório.
- **Sem política de retry.** Uma execução genuinamente instável falha o teste em vez
  de tentar de novo, por decisão deliberada — prefiro ver uma instabilidade a
  escondê-la, mas um projeto real ia querer um retry limitado e visível.
- **`noReset:false` deixa a suíte mais lenta do que precisaria** (~15-20s de overhead
  de reset do app por teste) em troca de determinismo — veja Decisões pelo bug que
  motivou essa escolha como padrão mais seguro.
- **A escrita de resultados do Allure contorna o pipeline de atributos do
  `Allure.NUnit`** (veja Decisões) — mesmo resultado no relatório, mas se o
  `Allure.NUnit` corrigir o bug de container numa versão futura, isso provavelmente
  pode ser simplificado de volta.
- **A suíte completa de 32 testes só roda 1x ao dia, não a cada PR** (veja Decisões) —
  escolha deliberada por causa da cota do plano trial, não descuido. A camada de
  smoke test (4 testes) cobre todo push/PR no lugar dela.
- **O GitHub desativa workflows agendados após 60 dias sem atividade no
  repositório** — uma limitação documentada do GitHub Actions que afeta
  especificamente a camada diária.

## Melhorias Futuras

- **Rodar a suíte completa de 32 testes em cada PR**, não só 1x ao dia — assim que um
  plano de maior capacidade na BrowserStack remover a preocupação com cota, o
  `scheduled-browserstack-run.yml` também poderia disparar em `pull_request`.
- **Sauce Labs / outros device clouds** — o branch da BrowserStack no `DriverFactory`
  já é um modelo pra isso; o mesmo padrão de `UseXyz` + capabilities específicas do
  fornecedor se aplicaria a qualquer outro grid na nuvem.
- **Execução paralela** — as fixtures já são independentes (usuários únicos, sem
  estado compartilhado), então habilitar o paralelismo do NUnit deve ser de baixo
  risco assim que a suposição de "um servidor Appium por execução" for revista (um
  grid consegue hospedar várias sessões simultâneas; o modelo atual de servidor local
  único precisaria de um servidor por worker).
- **Estratégia de retry** — um retry limitado e logado pra falhas genuinamente
  causadas por instabilidade de ambiente, distintas de falhas de asserção reais.
- **Test Data Builder** — o `TestUserFactory` é uma factory simples hoje; se as
  necessidades de dado de teste crescerem (múltiplos formatos de usuário, strings de
  edge-case), um builder manteria isso legível.
- **Integração via API** — se esse app algum dia expuser uma API de backend, semear
  ou verificar dados diretamente por ela seria mais rápido e confiável do que sempre
  passar pela UI pra configurar o cenário.
- **Testes de acessibilidade** — uma varredura baseada em axe (ou similar) por tela,
  especialmente relevante já que o app atualmente não tem nenhum identificador de
  acessibilidade.
