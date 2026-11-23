// [mirror] the status page's follow buttons - keep in sync with:
// - src/Workbench/Scripts/statusFollow.ts (the Workbench's stand-in, which changes the same elements the same way)
// Following a public status page by push (See StatusPublicPage.razor), the one script that plain page runs. The app's
// own service worker shows the pushes and opens the page when one is tapped (See service-worker.js).
(function () {
    function keyToBytes(base64Url) {
        const padding = '='.repeat((4 - base64Url.length % 4) % 4);
        const raw = atob((base64Url + padding).replace(/-/g, '+').replace(/_/g, '/'));
        return Uint8Array.from(raw, c => c.charCodeAt(0));
    }

    // Which pages this browser follows. The subscription is the browser's and may serve other pages and a signed-in
    // user's own alerts, so it says nothing about this page; storage can be off, and then the page just offers to follow.
    const followedKey = slug => `bit-status-following:${slug}`;

    function remember(slug, following) {
        try {
            following ? localStorage.setItem(followedKey(slug), '1') : localStorage.removeItem(followedKey(slug));
        } catch {
        }
    }

    function remembered(slug) {
        try {
            return localStorage.getItem(followedKey(slug)) === '1';
        } catch {
            return false;
        }
    }

    function say(text) {
        const note = document.querySelector('[data-follow-note]');
        if (note) {
            note.textContent = text;
            note.hidden = !text;
        }
    }

    function showFollowing(button, following) {
        button.querySelector('span').textContent = following ? button.dataset.following : button.dataset.followText;
        button.disabled = following;
        const unfollow = document.querySelector('[data-unfollow]');
        if (unfollow) {
            unfollow.hidden = !following;
        }
    }

    async function subscriptionOf() {
        const registration = await navigator.serviceWorker.getRegistration('/');
        return registration ? await registration.pushManager.getSubscription() : null;
    }

    async function post(button, action, subscription) {
        const keys = subscription.toJSON().keys;
        const response = await fetch(button.dataset.api + action, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                slug: button.dataset.slug,
                endpoint: subscription.endpoint,
                p256dh: keys.p256dh,
                auth: keys.auth,
                culture: button.dataset.culture,
                componentIds: []
            })
        });

        if (!response.ok) {
            throw new Error(`${action} answered ${response.status}`);
        }
    }

    async function follow(button) {
        try {
            if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
                // An iPhone or iPad sends push only to a page added to the Home Screen.
                say(/iPad|iPhone|iPod/.test(navigator.userAgent) ? button.dataset.ios : button.dataset.failed);
                return;
            }

            // Asked from the click itself, as browsers require.
            if (await Notification.requestPermission() !== 'granted') {
                say(button.dataset.failed);
                return;
            }

            const registration = await navigator.serviceWorker.register('/service-worker.js');
            await navigator.serviceWorker.ready;

            const subscription = await registration.pushManager.getSubscription()
                ?? await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyToBytes(button.dataset.key) });

            await post(button, 'Follow', subscription);

            remember(button.dataset.slug, true);
            say('');
            showFollowing(button, true);
        } catch (error) {
            console.error(error);
            say(button.dataset.failed);
        }
    }

    // Ends this page's pushes and keeps the browser's subscription, which other pages and the app's own alerts may use.
    async function unfollow(button) {
        try {
            const subscription = 'serviceWorker' in navigator ? await subscriptionOf() : null;
            if (subscription) {
                await post(button, 'Unfollow', subscription);
            }

            remember(button.dataset.slug, false);
            showFollowing(button, false);
            say(button.dataset.unfollowed);
        } catch (error) {
            console.error(error);
            say(button.dataset.failed);
        }
    }

    // A visitor who followed this page before sees it followed, with the way to stop.
    async function restore() {
        const button = document.querySelector('[data-follow]');
        if (!button || !remembered(button.dataset.slug) || !('serviceWorker' in navigator)) {
            return;
        }

        try {
            if (await subscriptionOf() && Notification.permission === 'granted') {
                showFollowing(button, true);
            } else {
                remember(button.dataset.slug, false);
            }
        } catch (error) {
            console.error(error);
        }
    }

    document.addEventListener('click', event => {
        const button = event.target.closest('[data-follow]');
        if (button) {
            follow(button);
            return;
        }

        if (event.target.closest('[data-unfollow]')) {
            const followButton = document.querySelector('[data-follow]');
            if (followButton) {
                unfollow(followButton);
            }
        }
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', restore);
    } else {
        restore();
    }
})();
