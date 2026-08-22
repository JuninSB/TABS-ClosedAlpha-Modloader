# Movimento: Closed Alpha x Steam

Esta análise foi feita diretamente nas duas cópias locais, sem copiar DLLs da versão Steam para o Alpha.

## O que o Steam faz

O caminho de unidades normais é dividido em sistemas:

1. `MovementHandler.BatchedFixedUpdate` aborta se `DataHandler.dead` ou `DataHandler.isGrounded` for falso.
2. O estado atual da animação seleciona um `MovementInstance`; cada estado tem seu próprio `force`.
3. O vetor de intenção vem de `GeneralInput.inputDirection`. O impulso é aplicado em `FixedUpdate` aos rigidbodies do `RigidbodyHolder` com `ForceMode.VelocityChange`, e não escrevendo `Transform.position`.
4. `Balance.BatchedUpdate` calcula o centro entre torso/cabeça e o centro entre os joelhos. Ele aplica correções com `AddForceAtPosition` nas posições dos joelhos para manter a postura e evitar que o corpo deslize/caia.
5. `GeneralInput.SetRotation` controla a direção visual separadamente da direção de movimento. Por isso o personagem pode olhar para frente enquanto recebe uma intenção de movimento para trás.

Portanto, a impressão de que “o pé empurra” vem da combinação de: gate de contato com o chão, impulso distribuído pelos rigidbodies, solver de pernas/equilíbrio e rotação separada. Não é uma única função de andar.

## O que existe no Alpha

O Alpha não possui `MovementHandler`, `RigidbodyHolder`, `Balance` ou `GeneralInput` do Steam. O caminho verificável é `UnitHandler` + `PhysicsAnimation`:

- `PhysicsAnimation.grounded` decide se `FixedUpdate` pode chamar `Walk()`.
- `Walk()` usa o `forwardDir` privado, `speed` e `turnMultiPlier` para aplicar força no torso e executar torques das pernas/braços.
- `PhysicsAnimation` também possui os rigidbodies de torso/pernas, mas o solver é diferente do Steam e não deve ser substituído por classes da versão moderna.

## Alteração aplicada no Tweaks

Quando `FISICA AVANCADA NA POSSE DA UNIDADE` está ligada, o mod continua usando `forwardDir`/`turnMultiPlier` do Alpha, mas agora verifica `PhysicsAnimation.grounded` em cada `FixedUpdate`. Se a unidade estiver no ar, a intenção é zerada e o mod não injeta força de caminhada; a queda e o pouso ficam sob controle do solver nativo. Quando volta a estar apoiada, a intenção é reaplicada.

Também foram adicionados dois assistentes opcionais, controlados no menu do Tweaks:

- `IMPULSO DISTRIBUIDO NOS RIGIDBODIES` aplica um impulso pequeno com `ForceMode.VelocityChange` a todos os rigidbodies dinâmicos da unidade, aproximando o modelo do `MovementHandler` moderno sem mover o `Transform`.
- `CORRECAO LEVE DE EQUILIBRIO` usa os rigidbodies `leftLeg`/`rightLeg` reais do Alpha e aplica uma correção horizontal no torso em direção ao centro de apoio. A intensidade é baixa e pode ser desligada se uma unidade especial não reagir bem.

Esses assistentes não substituem o `Walk()` nem inventam classes da Steam: eles complementam o solver que realmente existe nesta build.

Isso é deliberadamente conservador: importar o `MovementHandler` ou o `Balance` da Steam seria incompatível com o runtime e os tipos do Alpha. O próximo aprimoramento seguro é um solver opcional de tração/apoio usando apenas os rigidbodies de perna já presentes no Alpha, com raycasts que ignorem os próprios colliders da unidade.
