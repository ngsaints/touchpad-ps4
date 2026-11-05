# Touchpad PS4 como mouse

Aplicativo para usar o touchpad de um DualShock 4 como mouse no Windows. Inclui uma interface **UWP com efeito Acrylic**, um componente desktop para a área de notificação e o protótipo original em Windows Forms/PowerShell.

## Recursos da versão UWP

- Ativar/desativar pela janela, pelo ícone perto do relógio ou por `Ctrl+Alt+M`.
- Ocultar a interface e manter o motor ativo na área de notificação.
- Sensibilidade de **0,2× a 3×** e velocidade extra em gestos rápidos de **0% a +200%**.
- Clique por toque, clique físico e rolagem com dois dedos.
- `L1` + clique/toque para o botão direito.
- Preferências persistentes, restauração do movimento padrão e reconexão automática.

Os ajustes de movimento são aplicados apenas aos eventos gerados pelo DualShock. O aplicativo não altera as configurações do mouse físico no Windows. Ambos controlam o mesmo cursor.

## Plataforma

A interface é UWP; leitura contínua do controle, mouse global, atalho e ícone de bandeja são implementados por um auxiliar Win32 incluído no pacote (`runFullTrust`). Por isso, esta versão tem como destino **Windows desktop 10 1809 ou superior e Windows 11, x64**. Não é um pacote para Xbox ou outras famílias sem suporte a Win32.

## Compilar e instalar

Abra [`UWP/TouchpadPS4.sln`](UWP/TouchpadPS4.sln) no Visual Studio com ferramentas C# UWP e Windows SDK **10.0.19041.0**. Para gerar a distribuição assinada localmente:

```powershell
cd UWP
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Installer.ps1
```

O script gera uma pasta `Installer` com pacote APPX, dependências, certificado público e scripts para instalação. O certificado autoassinado é para distribuição local. A instalação exige que o destinatário confira e aceite o certificado. Nenhuma chave privada deve ser distribuída.

Consulte [`UWP/README.md`](UWP/README.md) para instruções de instalação, assinatura e comportamento em segundo plano.

## Verificação do motor

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\UWP\Build-Tray.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\UWP\Test-Engine.ps1
```

Os testes substituem a injeção de mouse por um registro em memória. Cobrem 12 cenários de gestos, liberação de cliques, precisão e velocidade.

## Estado atual

O auxiliar desktop compilou e os 12 testes do motor passaram. A interface e o pacote UWP ainda não foram compilados ou instalados nesta entrega por ausência das ferramentas UWP na máquina usada para o desenvolvimento. O funcionamento com controle físico e a distribuição em outros computadores ainda precisam ser validados. Não há instalador binário publicado neste repositório.

## Estrutura

- `UWP/`: projeto atual, interface, auxiliar, testes e scripts de empacotamento.
- `App.cs` e `AppEngine.cs`: protótipo original Windows Forms, preservado.
- `ds4-touchpad-mouse.ps1` e `ds4tm.cs`: protótipo original em PowerShell, preservado.

Os protótipos originais não receberam as correções e os ajustes do motor em `UWP/Tray/Engine.cs`. Arquivos compilados, certificados e preferências locais são excluídos do Git.
